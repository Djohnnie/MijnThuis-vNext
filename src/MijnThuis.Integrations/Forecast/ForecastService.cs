using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;

namespace MijnThuis.Integrations.Forecast;

public interface IForecastService
{
    Task<ForecastOverview> GetSolarForecastEstimateAsync(SolarPanelArray panels, CancellationToken cancellationToken = default);
}

/// <summary>
/// Solar production forecasts from Forecast.Solar (https://doc.forecast.solar). Results are cached to respect rate limits.
/// </summary>
internal sealed class ForecastService(
    HttpClient httpClient,
    IMemoryCache memoryCache,
    IOptions<ForecastOptions> options,
    TimeProvider timeProvider,
    ILogger<ForecastService> logger) : HttpIntegrationServiceBase(httpClient, logger), IForecastService
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    protected override string IntegrationName => IntegrationNames.Forecast;

    public Task<ForecastOverview> GetSolarForecastEstimateAsync(SolarPanelArray panels, CancellationToken cancellationToken = default)
    {
        const string operation = "Get solar forecast estimate";

        return ExecuteAsync(operation, async ct =>
        {
            var cacheKey = $"{nameof(ForecastService)}[{panels}]";
            if (memoryCache.TryGetValue(cacheKey, out ForecastOverview? cached) && cached is not null)
            {
                return cached;
            }

            var settings = options.Value;
            var p = panels;
            var keySegment = string.IsNullOrWhiteSpace(settings.ApiKey) ? string.Empty : $"{Uri.EscapeDataString(settings.ApiKey)}/";
            var uri = string.Create(CultureInfo.InvariantCulture,
                $"{keySegment}estimate/{p.Latitude}/{p.Longitude}/{p.Declination}/{p.Azimuth}/{p.Power}?damping={p.Damping}");

            var response = await GetJsonAsync<EstimateResponse>(operation, uri, ct);
            var overview = MapOverview(response.Result);

            memoryCache.Set(cacheKey, overview, settings.CacheDuration);

            return overview;
        }, cancellationToken);
    }

    private ForecastOverview MapOverview(EstimateResult result)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var periods = result.WattHoursPeriod
            .Select(x => new WattHourPeriod(DateTime.ParseExact(x.Key, TimestampFormat, CultureInfo.InvariantCulture), x.Value))
            .OrderBy(x => x.Timestamp)
            .ToList();
        var periodsToday = periods.Where(x => DateOnly.FromDateTime(x.Timestamp) == today).ToList();

        return new ForecastOverview
        {
            EstimatedWattHoursToday = result.WattHoursDay.GetValueOrDefault(today),
            EstimatedWattHoursTomorrow = result.WattHoursDay.GetValueOrDefault(today.AddDays(1)),
            EstimatedWattHoursDayAfterTomorrow = result.WattHoursDay.GetValueOrDefault(today.AddDays(2)),
            WattHourPeriods = periods,
            Sunrise = periodsToday.FirstOrDefault()?.Timestamp.TimeOfDay ?? TimeSpan.Zero,
            Sunset = periodsToday.LastOrDefault()?.Timestamp.TimeOfDay ?? TimeSpan.Zero
        };
    }

    private sealed class EstimateResponse
    {
        [JsonPropertyName("result")]
        public EstimateResult Result { get; set; } = new();
    }

    private sealed class EstimateResult
    {
        [JsonPropertyName("watt_hours_period")]
        public Dictionary<string, int> WattHoursPeriod { get; set; } = [];

        [JsonPropertyName("watt_hours_day")]
        public Dictionary<DateOnly, int> WattHoursDay { get; set; } = [];
    }
}
