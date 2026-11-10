using System.Globalization;
using System.Text.Json;
using Spectre.Console;

namespace TessieExplorer;

public sealed class VehiclePresenter
{
    private readonly IAnsiConsole console;
    private const double KmPerMile = 1.609344;

    private record Field(string Label, string Path, string Unit = "", bool Miles = false, bool Timestamp = false);

    private static readonly Field[] StateFields =
    [
        new("Locked", "vehicle_state.locked"),
        new("Charging status", "charge_state.charging_state"),
        new("Usable battery", "charge_state.usable_battery_level", "%"),
        new("Estimated remaining range", "charge_state.est_battery_range", "km", Miles: true),
        new("Inside temperature", "climate_state.inside_temp", "°C"),
        new("Outside temperature", "climate_state.outside_temp", "°C"),
        new("Preconditioning", "climate_state.is_preconditioning"),
        new("Cabin overheat protection", "climate_state.cabin_overheat_protection"),
        new("Charging current", "charge_state.charger_actual_current", "A"),
        new("Maximum charging current", "charge_state.charge_current_request_max", "A"),
        new("Charge limit", "charge_state.charge_limit_soc", "%"),
        new("Charge port open", "charge_state.charge_port_door_open"),
        new("Charge port latch", "charge_state.charge_port_latch"),
        new("Energy added", "charge_state.charge_energy_added", "kWh"),
        new("Rated range added", "charge_state.charge_miles_added_rated", "km", Miles: true)
    ];

    private static readonly Field[] LocationFields =
    [
        new("Address", "address"),
        new("Saved location", "saved_location")
    ];

    private static readonly Field[] HealthFields = [new("Battery health", "health_percent", "%")];

    private static readonly Field[] ChargeFields =
    [
        new("ID", "id"),
        new("Started", "started_at", Timestamp: true),
        new("Ended", "ended_at", Timestamp: true),
        new("Location", "location"),
        new("Saved location", "saved_location"),
        new("Supercharger", "is_supercharger"),
        new("Fast charger", "is_fast_charger"),
        new("Odometer", "odometer", "km"),
        new("Energy added", "energy_added", "kWh"),
        new("Energy used", "energy_used", "kWh"),
        new("Range added", "range_added", "km"),
        new("Starting battery", "starting_battery", "%"),
        new("Ending battery", "ending_battery", "%"),
        new("Distance since last charge", "distance_since_last_charge", "km")
    ];

    private static readonly Field[] DriveFields =
    [
        new("ID", "id"),
        new("Started", "started_at", Timestamp: true),
        new("Ended", "ended_at", Timestamp: true),
        new("Starting location", "starting_location"),
        new("Ending location", "ending_location"),
        new("Starting odometer", "starting_odometer", "km"),
        new("Ending odometer", "ending_odometer", "km"),
        new("Starting battery", "starting_battery", "%"),
        new("Ending battery", "ending_battery", "%"),
        new("Energy used", "energy_used", "kWh"),
        new("Rated range used", "rated_range_used", "km"),
        new("Average speed", "average_speed", "km/h"),
        new("Maximum speed", "max_speed", "km/h"),
        new("Distance", "odometer_distance", "km"),
        new("Average inside temperature", "average_inside_temperature", "°C"),
        new("Average outside temperature", "average_outside_temperature", "°C")
    ];

    public VehiclePresenter(IAnsiConsole console)
    {
        this.console = console;
    }

    public static string Title(VehicleSection section)
    {
        return section switch
        {
            VehicleSection.State => "Vehicle state",
            VehicleSection.BatteryHealth => "Battery health",
            VehicleSection.Location => "Location",
            VehicleSection.Charges => "Charge history",
            VehicleSection.Drives => "Drive history",
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
    }

    public void Display(VehicleSection section, JsonElement data, string vin)
    {
        console.Write(new Rule(Title(section)));
        if (section is VehicleSection.Charges or VehicleSection.Drives)
        {
            var results = data.GetProperty("results");
            if (results.GetArrayLength() == 0)
            {
                console.WriteLine("No history available.");
                return;
            }

            foreach (var item in results.EnumerateArray())
            {
                ShowFields(item, section == VehicleSection.Charges ? ChargeFields : DriveFields);
            }
            return;
        }

        if (section == VehicleSection.BatteryHealth)
        {
            foreach (var item in data.GetProperty("results").EnumerateArray())
            {
                if (TryGet(item, "vin", out var value) && value.ValueKind == JsonValueKind.String &&
                    string.Equals(value.GetString(), vin, StringComparison.OrdinalIgnoreCase))
                {
                    ShowFields(item, HealthFields);
                    return;
                }
            }

            console.WriteLine("Battery health unavailable for this VIN.");
            return;
        }

        ShowFields(data, section == VehicleSection.State ? StateFields : LocationFields);
    }

    private void ShowFields(JsonElement data, Field[] fields)
    {
        var table = new Table().AddColumn("Field").AddColumn("Value");
        foreach (var field in fields)
        {
            table.AddRow(Markup.Escape(field.Label), Markup.Escape(Format(data, field)));
        }
        console.Write(table);
    }

    private static string Format(JsonElement data, Field field)
    {
        if (!TryGet(data, field.Path, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "Unavailable";
        }

        if (field.Timestamp)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds))
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return "Unavailable";
                }
            }
            return "Unavailable";
        }

        if (field.Unit.Length > 0)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            {
                return "Unavailable";
            }
            return $"{(field.Miles ? number * KmPerMile : number).ToString("0.##", CultureInfo.InvariantCulture)} {field.Unit}";
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()) ? "Unavailable" : value.GetString()!,
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.Number => value.GetRawText(),
            _ => "Unavailable"
        };
    }

    private static bool TryGet(JsonElement data, string path, out JsonElement value)
    {
        value = data;
        foreach (var part in path.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value))
            {
                return false;
            }
        }
        return true;
    }
}
