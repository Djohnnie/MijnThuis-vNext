namespace MijnThuis.Integrations.Airconditioning;

public sealed record AirconditioningOverview
{
    public required bool IsOn { get; init; }
    public required decimal RoomTemperature { get; init; }
    public required decimal TargetTemperature { get; init; }
    public required string Mode { get; init; }
    public required string FanSpeed { get; init; }
}
