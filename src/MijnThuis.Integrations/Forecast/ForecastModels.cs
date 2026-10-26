namespace MijnThuis.Integrations.Forecast;

/// <summary>
/// Describes a solar panel array for which a production forecast is requested.
/// </summary>
/// <param name="Latitude">Latitude of the location, -90 (south) to 90 (north).</param>
/// <param name="Longitude">Longitude of the location, -180 (west) to 180 (east).</param>
/// <param name="Declination">Plane declination, 0 (horizontal) to 90 (vertical).</param>
/// <param name="Azimuth">Plane azimuth, -180 to 180 (-90 = east, 0 = south, 90 = west).</param>
/// <param name="Power">Installed modules power in kilo watt peak (kWp).</param>
/// <param name="Damping">Damping factor applied to the morning and evening values.</param>
public sealed record SolarPanelArray(decimal Latitude, decimal Longitude, decimal Declination, decimal Azimuth, decimal Power, byte Damping);

public sealed record ForecastOverview
{
    public required int EstimatedWattHoursToday { get; init; }
    public required int EstimatedWattHoursTomorrow { get; init; }
    public required int EstimatedWattHoursDayAfterTomorrow { get; init; }
    public required IReadOnlyList<WattHourPeriod> WattHourPeriods { get; init; }
    public required TimeSpan Sunrise { get; init; }
    public required TimeSpan Sunset { get; init; }
}

public sealed record WattHourPeriod(DateTime Timestamp, int WattHours);
