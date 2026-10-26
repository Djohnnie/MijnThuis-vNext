using System.Text.Json.Serialization;

namespace MijnThuis.Integrations.Heating.Remeha;

internal sealed class DashboardResponse
{
    [JsonPropertyName("appliances")]
    public List<Appliance> Appliances { get; set; } = [];
}

internal sealed class Appliance
{
    [JsonPropertyName("activeThermalMode")]
    public string? ActiveThermalMode { get; set; }

    [JsonPropertyName("outdoorTemperatureInformation")]
    public TemperatureInformation? OutdoorTemperatureInformation { get; set; }

    [JsonPropertyName("climateZones")]
    public List<ClimateZone> ClimateZones { get; set; } = [];
}

internal sealed class TemperatureInformation
{
    [JsonPropertyName("cloudOutdoorTemperature")]
    public decimal OutdoorTemperature { get; set; }
}

internal sealed class ClimateZone
{
    [JsonPropertyName("climateZoneId")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("roomTemperature")]
    public decimal RoomTemperature { get; set; }

    [JsonPropertyName("zoneMode")]
    public string? Mode { get; set; }

    [JsonPropertyName("setpoint")]
    public decimal Setpoint { get; set; }

    [JsonPropertyName("nextSetpoint")]
    public decimal NextSetpoint { get; set; }

    [JsonPropertyName("nextSwitchTime")]
    public DateTime? NextSwitchTime { get; set; }

    [JsonPropertyName("preHeat")]
    public Preheat? Preheat { get; set; }
}

internal sealed class Preheat
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }
}
