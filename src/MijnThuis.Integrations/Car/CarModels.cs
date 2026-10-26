namespace MijnThuis.Integrations.Car;

public sealed record CarOverview
{
    public required string State { get; init; }
    public required byte BatteryLevel { get; init; }
    public required int RemainingRange { get; init; }
    public required int TemperatureInside { get; init; }
    public required int TemperatureOutside { get; init; }
    public required bool IsLocked { get; init; }
    public required bool IsCharging { get; init; }
    public required bool IsPreconditioning { get; init; }
    public required bool IsCabinOverheatProtection { get; init; }
    public required int ChargingAmps { get; init; }
    public required int MaxChargingAmps { get; init; }
    public required int ChargeLimit { get; init; }
    public required bool IsChargePortOpen { get; init; }
    public required decimal ChargeEnergyAdded { get; init; }
    public required decimal ChargeRangeAdded { get; init; }
}

public sealed record BatteryHealth(decimal Percentage);

public sealed record CarLocation(string? Address, string? SavedLocation);

public sealed record CarCharge
{
    public required long Id { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime EndedAt { get; init; }
    public string? Location { get; init; }
    public string? LocationFriendlyName { get; init; }
    public required bool IsSupercharger { get; init; }
    public required bool IsFastCharger { get; init; }
    public required int Odometer { get; init; }
    public required decimal EnergyAdded { get; init; }
    public required decimal EnergyUsed { get; init; }
    public required int RangeAdded { get; init; }
    public required int BatteryLevelStart { get; init; }
    public required int BatteryLevelEnd { get; init; }
    public required int DistanceSinceLastCharge { get; init; }
}

public sealed record CarDrive
{
    public required long Id { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime EndedAt { get; init; }
    public string? StartingLocation { get; init; }
    public string? EndingLocation { get; init; }
    public required int StartingOdometer { get; init; }
    public required int EndingOdometer { get; init; }
    public required int StartingBattery { get; init; }
    public required int EndingBattery { get; init; }
    public required decimal EnergyUsed { get; init; }
    public required int RangeUsed { get; init; }
    public required int AverageSpeed { get; init; }
    public required int MaximumSpeed { get; init; }
    public required int Distance { get; init; }
    public required decimal AverageInsideTemperature { get; init; }
    public required decimal AverageOutsideTemperature { get; init; }
}
