namespace MijnThuis.Integrations.Solar.SolarEdge;

internal sealed record SiteMetadata(SiteLocation? Location);
internal sealed record SiteLocation(string? Timezone);
internal sealed record SolarDevice(string SerialNumber, decimal? Nameplate);
internal sealed record SiteSummary(EnergyTotal? Production, EnergyTotal? Consumption);
internal sealed record EnergyTotal(string Unit, decimal? Total, decimal? ToGrid, decimal? FromGrid);

internal sealed record PowerflowResponse(
    string Unit, DateTimeOffset? UpdatedAt, int RefreshRate,
    PowerflowElement? Pv, PowerflowElement? Load, PowerflowElement? Storage, PowerflowElement? Grid);

internal sealed record PowerflowElement(bool Active, decimal? Power, string? Status, decimal? ChargeLevel);

internal sealed record DestinationResponse(DestinationSeries? Production, DestinationSeries? Consumption);
internal sealed record DestinationSeries(string Unit, List<DestinationValue> Values);
internal sealed record DestinationValue(
    DateTimeOffset Timestamp, decimal? Total,
    decimal? ToSelfConsumption, decimal? ToStorage, decimal? ToGrid,
    decimal? FromPv, decimal? FromStorage, decimal? FromGrid);

internal sealed record Measurement(DateTimeOffset Timestamp, decimal? Value);
internal sealed record Measurements(string Unit, List<Measurement> Values);
internal sealed record StorageTelemetryResponse(Dictionary<string, StorageMetrics>? Storage);
internal sealed record StorageMetrics(
    Measurements? StateOfEnergy, Measurements? RemainingEnergy,
    Measurements? ChargePower, Measurements? DischargePower);
