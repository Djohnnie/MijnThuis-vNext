using System.Text.Json.Serialization;

namespace MijnThuis.Integrations.Solar;

public sealed record SolarOverview
{
    /// <summary>Current solar production in kW (SolarEdge) or W (Modbus proxy).</summary>
    public required decimal CurrentSolarPower { get; init; }

    /// <summary>Positive when the battery is charging, negative when discharging.</summary>
    public required decimal CurrentBatteryPower { get; init; }

    /// <summary>Positive when importing from the grid, negative when exporting.</summary>
    public required decimal CurrentGridPower { get; init; }

    public required decimal CurrentConsumptionPower { get; init; }

    public required decimal BatteryLevel { get; init; }

    public bool HasExportLimitation { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public int? RefreshRate { get; init; }
}

public sealed record BatteryLevel
{
    public required decimal Level { get; init; }
    public required decimal Health { get; init; }
    public decimal MaxEnergy { get; init; }
}

public sealed record EnergyOverview(decimal Produced, decimal Consumed, decimal Purchased, decimal Injected);

public sealed record EnergyProduced(decimal LastDayEnergy, decimal LastMonthEnergy);

public enum StorageDataRange
{
    Today,
    ThreeDays,
    Week,
    Month
}

public sealed record StorageData(IReadOnlyList<StorageDataEntry> Entries);

public sealed record StorageDataEntry(DateTime Timestamp, decimal ChargeState);

#region SolarEdge chart models (energy in Wh, power in kW)

public sealed class EnergyOverviewResponse
{
    [JsonPropertyName("summary")]
    public EnergySummary? Summary { get; set; }

    [JsonPropertyName("chart")]
    public EnergyChart? Chart { get; set; }
}

public sealed class EnergySummary
{
    [JsonPropertyName("production")]
    public decimal? Production { get; set; }
}

public sealed class EnergyChart
{
    [JsonPropertyName("measurements")]
    public List<EnergyMeasurement> Measurements { get; set; } = [];
}

public sealed class EnergyMeasurement
{
    [JsonPropertyName("measurementTime")]
    public DateTime MeasurementTime { get; set; }

    [JsonPropertyName("production")]
    public decimal? Production { get; set; }

    [JsonPropertyName("productionDistribution")]
    public ProductionDistribution? ProductionDistribution { get; set; }

    [JsonPropertyName("consumption")]
    public decimal? Consumption { get; set; }

    [JsonPropertyName("consumptionDistribution")]
    public ConsumptionDistribution? ConsumptionDistribution { get; set; }

    [JsonPropertyName("export")]
    public decimal? Export { get; set; }

    [JsonPropertyName("import")]
    public decimal? Import { get; set; }
}

public sealed class PowerOverviewResponse
{
    [JsonPropertyName("measurements")]
    public List<PowerMeasurement> Measurements { get; set; } = [];
}

public sealed class PowerMeasurement
{
    [JsonPropertyName("measurementTime")]
    public DateTime MeasurementTime { get; set; }

    [JsonPropertyName("production")]
    public decimal? Production { get; set; }

    [JsonPropertyName("productionDistribution")]
    public ProductionDistribution? ProductionDistribution { get; set; }

    [JsonPropertyName("consumption")]
    public decimal? Consumption { get; set; }

    [JsonPropertyName("consumptionDistribution")]
    public ConsumptionDistribution? ConsumptionDistribution { get; set; }

    [JsonPropertyName("storageLevel")]
    public decimal? StorageLevel { get; set; }

    [JsonPropertyName("export")]
    public decimal? Export { get; set; }

    [JsonPropertyName("import")]
    public decimal? Import { get; set; }
}

public sealed class ProductionDistribution
{
    [JsonPropertyName("productionToHome")]
    public decimal? ToHome { get; set; }

    [JsonPropertyName("productionToBattery")]
    public decimal? ToBattery { get; set; }

    [JsonPropertyName("productionToGrid")]
    public decimal? ToGrid { get; set; }
}

public sealed class ConsumptionDistribution
{
    [JsonPropertyName("consumptionFromBattery")]
    public decimal? FromBattery { get; set; }

    [JsonPropertyName("consumptionFromSolar")]
    public decimal? FromSolar { get; set; }

    [JsonPropertyName("consumptionFromGrid")]
    public decimal? FromGrid { get; set; }
}

#endregion

#region SolarEdge monitoring API responses

public sealed class BatteryOverviewResponse
{
    [JsonPropertyName("storageData")]
    public StorageResponse? Storage { get; set; }
}

public sealed class StorageResponse
{
    [JsonPropertyName("batteryCount")]
    public int BatteryCount { get; set; }

    [JsonPropertyName("batteries")]
    public List<BatteryResponse> Batteries { get; set; } = [];
}

public sealed class BatteryResponse
{
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("nameplate")]
    public decimal Nameplate { get; set; }

    [JsonPropertyName("telemetries")]
    public List<BatteryTelemetry> Telemetries { get; set; } = [];
}

public sealed class BatteryTelemetry
{
    [JsonPropertyName("timeStamp")]
    public DateTime TimeStamp { get; set; }

    [JsonPropertyName("power")]
    public decimal? Power { get; set; }

    [JsonPropertyName("batteryPercentageState")]
    public decimal? Level { get; set; }

    [JsonPropertyName("fullPackEnergyAvailable")]
    public decimal? EnergyAvailable { get; set; }
}

#endregion
