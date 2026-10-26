using Microsoft.Extensions.Logging;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Heating.Remeha;

namespace MijnThuis.Integrations.Heating;

public sealed record HeatingOverview
{
    public required string Mode { get; init; }
    public required decimal RoomTemperature { get; init; }
    public required decimal Setpoint { get; init; }
    public required decimal OutdoorTemperature { get; init; }
    public required decimal NextSetpoint { get; init; }
    public DateTime? NextSwitchTime { get; init; }
}

public interface IHeatingService
{
    Task<HeatingOverview> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task SetManualHeatingAsync(decimal temperature, CancellationToken cancellationToken = default);

    Task SetTemporaryOverrideHeatingAsync(decimal temperature, CancellationToken cancellationToken = default);

    Task SetScheduledHeatingAsync(int heatingProgramId = 1, CancellationToken cancellationToken = default);

    Task SetAntiFrostHeatingAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Remeha Home heating integration. Authentication is handled transparently by <see cref="RemehaAuthenticationHandler"/>.
/// </summary>
internal sealed class HeatingService(HttpClient httpClient, ILogger<HeatingService> logger)
    : HttpIntegrationServiceBase(httpClient, logger), IHeatingService
{
    private const string DashboardUri = "homes/dashboard";

    protected override string IntegrationName => IntegrationNames.Heating;

    public Task<HeatingOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        const string operation = "Get overview";

        return ExecuteAsync(operation, async ct =>
        {
            var (appliance, climateZone) = await GetClimateZoneAsync(operation, ct);

            return new HeatingOverview
            {
                Mode = climateZone.Preheat?.Active == true ? "Preheat" : climateZone.Mode ?? "Unknown",
                RoomTemperature = climateZone.RoomTemperature,
                Setpoint = climateZone.Setpoint,
                OutdoorTemperature = appliance.OutdoorTemperatureInformation?.OutdoorTemperature ?? 0M,
                NextSetpoint = climateZone.NextSetpoint,
                NextSwitchTime = climateZone.NextSwitchTime
            };
        }, cancellationToken);
    }

    public Task SetManualHeatingAsync(decimal temperature, CancellationToken cancellationToken = default)
        => SetZoneModeAsync("Set manual heating", "manual", new { roomTemperatureSetPoint = temperature }, cancellationToken);

    public Task SetTemporaryOverrideHeatingAsync(decimal temperature, CancellationToken cancellationToken = default)
        => SetZoneModeAsync("Set temporary override heating", "temporary-override", new { roomTemperatureSetPoint = temperature }, cancellationToken);

    public Task SetScheduledHeatingAsync(int heatingProgramId = 1, CancellationToken cancellationToken = default)
        => SetZoneModeAsync("Set scheduled heating", "schedule", new { heatingProgramId }, cancellationToken);

    public Task SetAntiFrostHeatingAsync(CancellationToken cancellationToken = default)
        => SetZoneModeAsync("Set anti-frost heating", "anti-frost", new { }, cancellationToken);

    private Task SetZoneModeAsync(string operation, string mode, object body, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, async ct =>
        {
            var (_, climateZone) = await GetClimateZoneAsync(operation, ct);
            await SendAsync(operation, HttpMethod.Post, $"climate-zones/{Uri.EscapeDataString(climateZone.Id)}/modes/{mode}", body, ct);
        }, cancellationToken);
    }

    private async Task<(Appliance Appliance, ClimateZone ClimateZone)> GetClimateZoneAsync(string operation, CancellationToken cancellationToken)
    {
        var dashboard = await GetJsonAsync<DashboardResponse>(operation, DashboardUri, cancellationToken);

        var appliance = dashboard.Appliances.FirstOrDefault()
            ?? throw ResponseError(operation, "No heating appliance was found on this account.");
        var climateZone = appliance.ClimateZones.FirstOrDefault()
            ?? throw ResponseError(operation, "The heating appliance has no climate zones.");

        return (appliance, climateZone);
    }
}
