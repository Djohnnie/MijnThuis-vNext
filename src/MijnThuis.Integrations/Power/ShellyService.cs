using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Exceptions;

namespace MijnThuis.Integrations.Power;

public enum PowerSwitch
{
    Tv,
    Bureau,
    Vijver
}

public sealed record PowerSwitchOverview(PowerSwitch Switch, bool IsOn, decimal Power);

public interface IShellyService
{
    Task<PowerSwitchOverview> GetPowerSwitchAsync(PowerSwitch powerSwitch, CancellationToken cancellationToken = default);

    Task SetPowerSwitchAsync(PowerSwitch powerSwitch, bool isOn, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shelly (Gen 1) smart plugs through their local HTTP API. One client is shared by all plugs;
/// the resilience pipeline is selected per plug (authority) so one offline plug doesn't affect the others.
/// </summary>
internal sealed class ShellyService(HttpClient httpClient, IOptions<PowerSwitchOptions> options, ILogger<ShellyService> logger)
    : HttpIntegrationServiceBase(httpClient, logger), IShellyService
{
    protected override string IntegrationName => IntegrationNames.PowerSwitches;

    public Task<PowerSwitchOverview> GetPowerSwitchAsync(PowerSwitch powerSwitch, CancellationToken cancellationToken = default)
    {
        var operation = $"Get {powerSwitch} power switch";

        return ExecuteAsync(operation, async ct =>
        {
            var status = await GetJsonAsync<StatusResponse>(operation, $"{GetBaseAddress(powerSwitch, operation)}/status", ct);

            return new PowerSwitchOverview(
                powerSwitch,
                status.Relays.FirstOrDefault()?.IsOn ?? false,
                status.Meters.FirstOrDefault()?.Power ?? 0M);
        }, cancellationToken);
    }

    public Task SetPowerSwitchAsync(PowerSwitch powerSwitch, bool isOn, CancellationToken cancellationToken = default)
    {
        var operation = $"Turn {(isOn ? "on" : "off")} {powerSwitch} power switch";

        return ExecuteAsync(operation, ct =>
            SendAsync(operation, HttpMethod.Get, $"{GetBaseAddress(powerSwitch, operation)}/relay/0?turn={(isOn ? "on" : "off")}", body: null, ct),
            cancellationToken);
    }

    private string GetBaseAddress(PowerSwitch powerSwitch, string operation)
    {
        var settings = options.Value;
        var (address, key) = powerSwitch switch
        {
            PowerSwitch.Tv => (settings.TvBaseAddress, "TV_POWER_SWITCH_BASE_ADDRESS"),
            PowerSwitch.Bureau => (settings.BureauBaseAddress, "BUREAU_POWER_SWITCH_BASE_ADDRESS"),
            PowerSwitch.Vijver => (settings.VijverBaseAddress, "VIJVER_POWER_SWITCH_BASE_ADDRESS"),
            _ => throw new ArgumentOutOfRangeException(nameof(powerSwitch), powerSwitch, null)
        };

        return string.IsNullOrWhiteSpace(address)
            ? throw new IntegrationConfigurationException(IntegrationName, operation, $"The '{key}' setting is missing.")
            : address.TrimEnd('/');
    }

    private sealed class StatusResponse
    {
        [JsonPropertyName("relays")]
        public List<Relay> Relays { get; set; } = [];

        [JsonPropertyName("meters")]
        public List<Meter> Meters { get; set; } = [];
    }

    private sealed class Relay
    {
        [JsonPropertyName("ison")]
        public bool IsOn { get; set; }
    }

    private sealed class Meter
    {
        [JsonPropertyName("power")]
        public decimal Power { get; set; }
    }
}
