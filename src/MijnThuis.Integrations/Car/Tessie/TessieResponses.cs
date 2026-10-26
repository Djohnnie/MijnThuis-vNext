using System.Text.Json.Serialization;

namespace MijnThuis.Integrations.Car.Tessie;

internal sealed class CommandResponse
{
    [JsonPropertyName("result")]
    public bool Result { get; set; }
}

internal sealed class StateResponse
{
    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("charge_state")]
    public ChargeState ChargeState { get; set; } = new();

    [JsonPropertyName("climate_state")]
    public ClimateState ClimateState { get; set; } = new();

    [JsonPropertyName("vehicle_state")]
    public VehicleState VehicleState { get; set; } = new();
}

internal sealed class ChargeState
{
    [JsonPropertyName("usable_battery_level")]
    public byte BatteryLevel { get; set; }

    [JsonPropertyName("est_battery_range")]
    public decimal BatteryRange { get; set; }

    [JsonPropertyName("charging_state")]
    public string? ChargingState { get; set; }

    [JsonPropertyName("charger_actual_current")]
    public int ChargingAmps { get; set; }

    [JsonPropertyName("charge_current_request_max")]
    public int MaxChargingAmps { get; set; }

    [JsonPropertyName("charge_limit_soc")]
    public int ChargeLimit { get; set; }

    [JsonPropertyName("charge_port_door_open")]
    public bool IsChargePortOpen { get; set; }

    [JsonPropertyName("charge_energy_added")]
    public decimal ChargeEnergyAdded { get; set; }

    [JsonPropertyName("charge_miles_added_rated")]
    public decimal ChargeMilesAdded { get; set; }
}

internal sealed class ClimateState
{
    [JsonPropertyName("inside_temp")]
    public decimal InsideTemp { get; set; }

    [JsonPropertyName("outside_temp")]
    public decimal OutsideTemp { get; set; }

    [JsonPropertyName("is_preconditioning")]
    public bool IsPreconditioning { get; set; }

    [JsonPropertyName("cabin_overheat_protection")]
    public string? CabinOverheatProtection { get; set; }
}

internal sealed class VehicleState
{
    [JsonPropertyName("locked")]
    public bool Locked { get; set; }
}

internal sealed class LocationResponse
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("saved_location")]
    public string? SavedLocation { get; set; }
}

internal sealed class ResultsResponse<T>
{
    [JsonPropertyName("results")]
    public List<T> Results { get; set; } = [];
}

internal sealed class VehicleBatteryHealth
{
    [JsonPropertyName("vin")]
    public string? Vin { get; set; }

    [JsonPropertyName("health_percent")]
    public decimal Percentage { get; set; }
}

internal sealed class ChargeResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("started_at")]
    public long StartedAt { get; set; }

    [JsonPropertyName("ended_at")]
    public long EndedAt { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("saved_location")]
    public string? LocationFriendlyName { get; set; }

    [JsonPropertyName("is_supercharger")]
    public bool IsSupercharger { get; set; }

    [JsonPropertyName("is_fast_charger")]
    public bool IsFastCharger { get; set; }

    [JsonPropertyName("odometer")]
    public decimal Odometer { get; set; }

    [JsonPropertyName("energy_added")]
    public decimal EnergyAdded { get; set; }

    [JsonPropertyName("energy_used")]
    public decimal EnergyUsed { get; set; }

    [JsonPropertyName("miles_added")]
    public decimal RangeAdded { get; set; }

    [JsonPropertyName("starting_battery")]
    public int BatteryLevelStart { get; set; }

    [JsonPropertyName("ending_battery")]
    public int BatteryLevelEnd { get; set; }

    [JsonPropertyName("since_last_charge")]
    public decimal DistanceSinceLastCharge { get; set; }
}

internal sealed class DriveResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("started_at")]
    public long StartedAt { get; set; }

    [JsonPropertyName("ended_at")]
    public long EndedAt { get; set; }

    [JsonPropertyName("starting_location")]
    public string? StartingLocation { get; set; }

    [JsonPropertyName("ending_location")]
    public string? EndingLocation { get; set; }

    [JsonPropertyName("starting_odometer")]
    public decimal StartingOdometer { get; set; }

    [JsonPropertyName("ending_odometer")]
    public decimal EndingOdometer { get; set; }

    [JsonPropertyName("starting_battery")]
    public int StartingBattery { get; set; }

    [JsonPropertyName("ending_battery")]
    public int EndingBattery { get; set; }

    [JsonPropertyName("energy_used")]
    public decimal EnergyUsed { get; set; }

    [JsonPropertyName("rated_range_used")]
    public decimal RangeUsed { get; set; }

    [JsonPropertyName("average_speed")]
    public int AverageSpeed { get; set; }

    [JsonPropertyName("max_speed")]
    public int MaximumSpeed { get; set; }

    [JsonPropertyName("odometer_distance")]
    public decimal Distance { get; set; }

    [JsonPropertyName("average_inside_temperature")]
    public decimal AverageInsideTemperature { get; set; }

    [JsonPropertyName("average_outside_temperature")]
    public decimal AverageOutsideTemperature { get; set; }
}
