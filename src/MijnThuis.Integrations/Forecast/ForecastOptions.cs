using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Forecast;

public sealed class ForecastOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("FORECAST_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = "https://api.forecast.solar/";

    /// <summary>
    /// Optional personal API key. When empty, the (rate limited) public API is used.
    /// </summary>
    [Secret]
    [ConfigurationKeyName("FORECAST_API_KEY")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// How long a forecast is cached. Forecast.Solar applies strict rate limits, so keep this reasonably high.
    /// </summary>
    [ConfigurationKeyName("FORECAST_CACHE_DURATION")]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(30);
}
