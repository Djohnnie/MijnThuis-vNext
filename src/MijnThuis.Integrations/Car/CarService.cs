using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Car.Tessie;
using MijnThuis.Integrations.Common;

namespace MijnThuis.Integrations.Car;

public interface ICarService
{
    Task<CarOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<BatteryHealth> GetBatteryHealthAsync(CancellationToken cancellationToken = default);
    Task<CarLocation> GetLocationAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CarCharge>> GetChargesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CarDrive>> GetDrivesAsync(CancellationToken cancellationToken = default);
    Task LockAsync(CancellationToken cancellationToken = default);
    Task UnlockAsync(CancellationToken cancellationToken = default);
    Task HonkAsync(CancellationToken cancellationToken = default);
    Task FartAsync(CancellationToken cancellationToken = default);
    Task PreheatAsync(CancellationToken cancellationToken = default);
    Task StartChargingAsync(int chargingAmps, CancellationToken cancellationToken = default);
    Task StopChargingAsync(CancellationToken cancellationToken = default);
    Task SetCabinOverheatProtectionAsync(bool enable, bool fanOnly, CancellationToken cancellationToken = default);
    Task SetCabinOverheatProtectionTemperatureAsync(int temperature, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tesla vehicle integration through the Tessie API (https://developer.tessie.com).
/// </summary>
internal sealed class CarService(HttpClient httpClient, IOptions<CarOptions> options, ILogger<CarService> logger)
    : HttpIntegrationServiceBase(httpClient, logger), ICarService
{
    private const decimal KilometersPerMile = 1.60934M;

    private string Vin => options.Value.VinNumber;

    protected override string IntegrationName => IntegrationNames.Car;

    public Task<CarOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Get overview", async ct =>
        {
            var result = await GetJsonAsync<StateResponse>("Get overview", $"{Vin}/state", ct);

            return new CarOverview
            {
                State = result.State ?? "Unknown",
                IsLocked = result.VehicleState.Locked,
                IsCharging = result.ChargeState.ChargingState == "Charging",
                BatteryLevel = result.ChargeState.BatteryLevel,
                RemainingRange = (int)(result.ChargeState.BatteryRange * KilometersPerMile),
                TemperatureInside = (int)result.ClimateState.InsideTemp,
                TemperatureOutside = (int)result.ClimateState.OutsideTemp,
                IsPreconditioning = result.ClimateState.IsPreconditioning,
                IsCabinOverheatProtection = result.ClimateState.CabinOverheatProtection == "On",
                ChargingAmps = result.ChargeState.ChargingAmps,
                MaxChargingAmps = result.ChargeState.MaxChargingAmps,
                ChargeLimit = result.ChargeState.ChargeLimit,
                IsChargePortOpen = result.ChargeState.IsChargePortOpen,
                ChargeEnergyAdded = result.ChargeState.ChargeEnergyAdded,
                ChargeRangeAdded = result.ChargeState.ChargeMilesAdded * KilometersPerMile
            };
        }, cancellationToken);
    }

    public Task<BatteryHealth> GetBatteryHealthAsync(CancellationToken cancellationToken = default)
    {
        const string operation = "Get battery health";

        return ExecuteAsync(operation, async ct =>
        {
            var result = await GetJsonAsync<ResultsResponse<VehicleBatteryHealth>>(operation, "battery_health", ct);
            var health = result.Results.SingleOrDefault(x => string.Equals(x.Vin, Vin, StringComparison.OrdinalIgnoreCase))
                ?? throw ResponseError(operation, $"No battery health data found for VIN '{Vin}'.");

            return new BatteryHealth(health.Percentage);
        }, cancellationToken);
    }

    public Task<CarLocation> GetLocationAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Get location", async ct =>
        {
            var result = await GetJsonAsync<LocationResponse>("Get location", $"{Vin}/location", ct);
            return new CarLocation(result.Address, result.SavedLocation);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<CarCharge>> GetChargesAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<IReadOnlyList<CarCharge>>("Get charges", async ct =>
        {
            var result = await GetJsonAsync<ResultsResponse<ChargeResult>>("Get charges", $"{Vin}/charges?distance_format=km&format=json", ct);

            return result.Results.Select(c => new CarCharge
            {
                Id = c.Id,
                StartedAt = FromUnixTime(c.StartedAt),
                EndedAt = FromUnixTime(c.EndedAt),
                Location = c.Location,
                LocationFriendlyName = c.LocationFriendlyName,
                IsSupercharger = c.IsSupercharger,
                IsFastCharger = c.IsFastCharger,
                Odometer = (int)Math.Round(c.Odometer),
                EnergyAdded = c.EnergyAdded,
                EnergyUsed = c.EnergyUsed,
                RangeAdded = (int)Math.Round(c.RangeAdded),
                BatteryLevelStart = c.BatteryLevelStart,
                BatteryLevelEnd = c.BatteryLevelEnd,
                DistanceSinceLastCharge = (int)Math.Round(c.DistanceSinceLastCharge)
            }).ToList();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<CarDrive>> GetDrivesAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<IReadOnlyList<CarDrive>>("Get drives", async ct =>
        {
            var result = await GetJsonAsync<ResultsResponse<DriveResult>>("Get drives", $"{Vin}/drives?distance_format=km&temperature_format=c", ct);

            return result.Results.Select(d => new CarDrive
            {
                Id = d.Id,
                StartedAt = FromUnixTime(d.StartedAt),
                EndedAt = FromUnixTime(d.EndedAt),
                StartingLocation = d.StartingLocation,
                EndingLocation = d.EndingLocation,
                StartingOdometer = (int)Math.Round(d.StartingOdometer),
                EndingOdometer = (int)Math.Round(d.EndingOdometer),
                StartingBattery = d.StartingBattery,
                EndingBattery = d.EndingBattery,
                EnergyUsed = d.EnergyUsed,
                RangeUsed = (int)Math.Round(d.RangeUsed),
                AverageSpeed = d.AverageSpeed,
                MaximumSpeed = d.MaximumSpeed,
                Distance = (int)Math.Round(d.Distance),
                AverageInsideTemperature = d.AverageInsideTemperature,
                AverageOutsideTemperature = d.AverageOutsideTemperature
            }).ToList();
        }, cancellationToken);
    }

    public Task LockAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync("Lock", "lock", cancellationToken);

    public Task UnlockAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync("Unlock", "unlock", cancellationToken);

    public Task HonkAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync("Honk", "honk", cancellationToken);

    public Task FartAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync("Fart", "remote_boombox", cancellationToken);

    public Task PreheatAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync("Preheat", "start_climate", cancellationToken);

    public Task StartChargingAsync(int chargingAmps, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Start charging", async ct =>
        {
            await SendCommandAsync("Set charging amps", $"set_charging_amps?amps={chargingAmps}", ct);
            await SendCommandAsync("Start charging", "start_charging", ct);
        }, cancellationToken);
    }

    public Task StopChargingAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync("Stop charging", "stop_charging", cancellationToken);

    public Task SetCabinOverheatProtectionAsync(bool enable, bool fanOnly, CancellationToken cancellationToken = default)
        => SendCommandAsync("Set cabin overheat protection", $"set_cabin_overheat_protection?on={ToQuery(enable)}&fan_only={ToQuery(fanOnly)}", cancellationToken);

    public Task SetCabinOverheatProtectionTemperatureAsync(int temperature, CancellationToken cancellationToken = default)
        => SendCommandAsync("Set cabin overheat protection temperature", $"set_cop_temp?temperature={temperature}", cancellationToken);

    private Task SendCommandAsync(string operation, string command, CancellationToken cancellationToken)
    {
        return ExecuteAsync(operation, async ct =>
        {
            var response = await GetJsonAsync<CommandResponse>(operation, $"{Vin}/command/{command}", ct);

            if (!response.Result)
            {
                throw ResponseError(operation, "The vehicle did not accept the command (is it asleep or offline?).");
            }
        }, cancellationToken);
    }

    private static DateTime FromUnixTime(long seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().DateTime;

    private static string ToQuery(bool value) => value ? "true" : "false";
}
