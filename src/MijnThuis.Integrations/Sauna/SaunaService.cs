using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Sauna;

public sealed class SaunaOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("SAUNA_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = string.Empty;

    [Required]
    [Secret]
    [ConfigurationKeyName("SAUNA_API_AUTH_TOKEN")]
    public string AuthToken { get; set; } = string.Empty;
}

public enum SaunaState
{
    Off,
    Sauna,
    Infrared
}

public interface ISaunaService
{
    Task<SaunaState> GetStateAsync(CancellationToken cancellationToken = default);

    Task<int> GetInsideTemperatureAsync(CancellationToken cancellationToken = default);

    Task<int> GetOutsideTemperatureAsync(CancellationToken cancellationToken = default);

    Task<decimal> GetPowerUsageAsync(CancellationToken cancellationToken = default);

    /// <returns>The identifier of the active session, or <c>null</c> when there is none.</returns>
    Task<string?> GetActiveSessionAsync(CancellationToken cancellationToken = default);

    Task StartSaunaAsync(CancellationToken cancellationToken = default);

    Task StartInfraredAsync(CancellationToken cancellationToken = default);

    Task StopSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

internal sealed class SaunaService(HttpClient httpClient, ILogger<SaunaService> logger)
    : HttpIntegrationServiceBase(httpClient, logger), ISaunaService
{
    protected override string IntegrationName => IntegrationNames.Sauna;

    public Task<SaunaState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        const string operation = "Get state";

        return ExecuteAsync(operation, async ct =>
        {
            var result = await GetContentAsync<StateResponse>(operation, "sensors/state", ct);

            return result switch
            {
                { IsSaunaOn: true } => SaunaState.Sauna,
                { IsInfraredOn: true } => SaunaState.Infrared,
                _ => SaunaState.Off
            };
        }, cancellationToken);
    }

    public Task<int> GetInsideTemperatureAsync(CancellationToken cancellationToken = default)
        => GetTemperatureAsync("Get inside temperature", "sensors/temperature/sauna", cancellationToken);

    public Task<int> GetOutsideTemperatureAsync(CancellationToken cancellationToken = default)
        => GetTemperatureAsync("Get outside temperature", "sensors/temperature/outside", cancellationToken);

    public Task<decimal> GetPowerUsageAsync(CancellationToken cancellationToken = default)
    {
        const string operation = "Get power usage";

        return ExecuteAsync(operation, async ct =>
        {
            var result = await GetContentAsync<PowerResponse>(operation, "sensors/power/sauna", ct);
            return result.SaunaPowerUsage + result.InfraredPowerUsage;
        }, cancellationToken);
    }

    public Task<string?> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        const string operation = "Get active session";

        return ExecuteAsync(operation, async ct =>
        {
            var result = await GetJsonOrDefaultAsync<ContentResponse<ActiveSessionResponse>>(operation, "sauna/sessions/active", ct);
            return string.IsNullOrWhiteSpace(result?.Content?.SessionId) ? null : result.Content.SessionId;
        }, cancellationToken);
    }

    public Task StartSaunaAsync(CancellationToken cancellationToken = default)
        => SendAsync("Start sauna", HttpMethod.Post, "sauna/sessions/quickstart", new QuickStartRequest { IsSauna = true }, cancellationToken);

    public Task StartInfraredAsync(CancellationToken cancellationToken = default)
        => SendAsync("Start infrared", HttpMethod.Post, "sauna/sessions/quickstart", new QuickStartRequest { IsInfrared = true }, cancellationToken);

    public Task StopSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return SendAsync("Stop session", HttpMethod.Put, $"sauna/sessions/{Uri.EscapeDataString(sessionId)}/cancel", body: null, cancellationToken);
    }

    private Task<int> GetTemperatureAsync(string operation, string requestUri, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, async ct =>
        {
            var result = await GetContentAsync<TemperatureResponse>(operation, requestUri, ct);
            return result.Temperature;
        }, cancellationToken);
    }

    private async Task<T> GetContentAsync<T>(string operation, string requestUri, CancellationToken cancellationToken)
    {
        var result = await GetJsonAsync<ContentResponse<T>>(operation, requestUri, cancellationToken);
        return result.Content ?? throw ResponseError(operation, "The response did not contain any content.");
    }

    private sealed class ContentResponse<T>
    {
        [JsonPropertyName("content")]
        public T? Content { get; set; }
    }

    private sealed class StateResponse
    {
        [JsonPropertyName("isSaunaOn")]
        public bool IsSaunaOn { get; set; }

        [JsonPropertyName("isInfraredOn")]
        public bool IsInfraredOn { get; set; }
    }

    private sealed class TemperatureResponse
    {
        [JsonPropertyName("temperature")]
        public int Temperature { get; set; }
    }

    private sealed class PowerResponse
    {
        [JsonPropertyName("saunaPowerUsage")]
        public decimal SaunaPowerUsage { get; set; }

        [JsonPropertyName("infraredPowerUsage")]
        public decimal InfraredPowerUsage { get; set; }
    }

    private sealed class ActiveSessionResponse
    {
        [JsonPropertyName("sessionId")]
        public string? SessionId { get; set; }
    }

    private sealed class QuickStartRequest
    {
        [JsonPropertyName("isSauna")]
        public bool IsSauna { get; set; }

        [JsonPropertyName("isInfrared")]
        public bool IsInfrared { get; set; }
    }
}
