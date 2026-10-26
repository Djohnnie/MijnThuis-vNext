using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MijnThuis.Integrations.Common;

namespace MijnThuis.Integrations.Lamps;

public sealed record Lamp
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? Group { get; init; }
    public string? Location { get; init; }
    public required bool IsConnected { get; init; }
    public required bool IsOn { get; init; }

    /// <summary>
    /// Brightness between 0 and 1.
    /// </summary>
    public required decimal Brightness { get; init; }
}

public sealed record LampActionResult(string Id, string Label, string Status);

public interface ILampsService
{
    /// <summary>
    /// Selector that targets every lamp on the account.
    /// See https://api.developer.lifx.com/reference/selectors for other selectors (e.g. "id:d073d5..", "group:Living").
    /// </summary>
    const string AllLamps = "all";

    Task<IReadOnlyList<Lamp>> GetLampsAsync(string selector = AllLamps, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LampActionResult>> SetPowerAsync(bool isOn, string selector = AllLamps, CancellationToken cancellationToken = default);

    /// <param name="brightness">Brightness between 0 and 1.</param>
    Task<IReadOnlyList<LampActionResult>> SetBrightnessAsync(decimal brightness, string selector = AllLamps, CancellationToken cancellationToken = default);
}

/// <summary>
/// LIFX lamps through the LIFX HTTP API (https://api.developer.lifx.com).
/// </summary>
internal sealed class LampsService(HttpClient httpClient, ILogger<LampsService> logger)
    : HttpIntegrationServiceBase(httpClient, logger), ILampsService
{
    protected override string IntegrationName => IntegrationNames.Lamps;

    public Task<IReadOnlyList<Lamp>> GetLampsAsync(string selector = ILampsService.AllLamps, CancellationToken cancellationToken = default)
    {
        const string operation = "Get lamps";

        return ExecuteAsync<IReadOnlyList<Lamp>>(operation, async ct =>
        {
            var lights = await GetJsonAsync<List<LightResponse>>(operation, $"lights/{Uri.EscapeDataString(selector)}", ct);

            return lights.Select(x => new Lamp
            {
                Id = x.Id,
                Label = x.Label ?? x.Id,
                Group = x.Group?.Name,
                Location = x.Location?.Name,
                IsConnected = x.Connected,
                IsOn = x.Power == "on",
                Brightness = x.Brightness
            }).ToList();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<LampActionResult>> SetPowerAsync(bool isOn, string selector = ILampsService.AllLamps, CancellationToken cancellationToken = default)
        => SetStateAsync("Set power", selector, new SetStateRequest { Power = isOn ? "on" : "off" }, cancellationToken);

    public Task<IReadOnlyList<LampActionResult>> SetBrightnessAsync(decimal brightness, string selector = ILampsService.AllLamps, CancellationToken cancellationToken = default)
        => SetStateAsync("Set brightness", selector, new SetStateRequest { Power = "on", Brightness = Math.Clamp(brightness, 0M, 1M) }, cancellationToken);

    private Task<IReadOnlyList<LampActionResult>> SetStateAsync(string operation, string selector, SetStateRequest request, CancellationToken cancellationToken)
    {
        return ExecuteAsync<IReadOnlyList<LampActionResult>>(operation, async ct =>
        {
            var response = await SendJsonAsync<SetStateResponse>(operation, HttpMethod.Put, $"lights/{Uri.EscapeDataString(selector)}/state", request, ct);

            return response.Results.Select(x => new LampActionResult(x.Id, x.Label ?? x.Id, x.Status ?? "unknown")).ToList();
        }, cancellationToken);
    }

    private sealed class LightResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("connected")]
        public bool Connected { get; set; }

        [JsonPropertyName("power")]
        public string? Power { get; set; }

        [JsonPropertyName("brightness")]
        public decimal Brightness { get; set; }

        [JsonPropertyName("group")]
        public NamedEntity? Group { get; set; }

        [JsonPropertyName("location")]
        public NamedEntity? Location { get; set; }
    }

    private sealed class NamedEntity
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class SetStateRequest
    {
        [JsonPropertyName("power")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Power { get; set; }

        [JsonPropertyName("brightness")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Brightness { get; set; }

        [JsonPropertyName("duration")]
        public decimal Duration { get; set; } = 1;
    }

    private sealed class SetStateResponse
    {
        [JsonPropertyName("results")]
        public List<SetStateResult> Results { get; set; } = [];
    }

    private sealed class SetStateResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
