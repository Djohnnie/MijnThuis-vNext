using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Solar;

public sealed class SolarOptions : IValidatableObject
{
    /// <summary>
    /// SolarEdge V2 monitoring API, including the /v2/ path.
    /// </summary>
    [Required]
    [Url]
    [ConfigurationKeyName("SOLAR_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = "https://monitoringapi.solaredge.com/v2/";

    /// <summary>New V2 Fleet API key, not a legacy site key. Leave empty when using OAuth.</summary>
    [Secret]
    [ConfigurationKeyName("SOLAR_API_AUTH_TOKEN")]
    public string? AuthToken { get; set; }

    [Required]
    [ConfigurationKeyName("SOLAR_SITE_ID")]
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth application credentials for homeowner site access. Fleet users configure AuthToken instead.
    /// </summary>
    [ConfigurationKeyName("SOLAR_OAUTH_CLIENT_ID")]
    public string? ClientId { get; set; }

    [Secret]
    [ConfigurationKeyName("SOLAR_OAUTH_CLIENT_SECRET")]
    public string? ClientSecret { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(BaseAddress, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.AbsolutePath.TrimEnd('/').EndsWith("/v2", StringComparison.Ordinal))
        {
            yield return new ValidationResult("SOLAR_API_BASE_ADDRESS must be an HTTPS V2 API URL, normally https://monitoringapi.solaredge.com/v2/.", [nameof(BaseAddress)]);
        }

        if (string.IsNullOrWhiteSpace(AuthToken) && (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret)))
        {
            yield return new ValidationResult("Configure a new V2 Fleet API key (SOLAR_API_AUTH_TOKEN), or both SOLAR_OAUTH_CLIENT_ID and SOLAR_OAUTH_CLIENT_SECRET for homeowner OAuth access. V1 keys and app passwords cannot be used.", [nameof(AuthToken), nameof(ClientId), nameof(ClientSecret)]);
        }

        if (!string.IsNullOrWhiteSpace(AuthToken) && (!string.IsNullOrWhiteSpace(ClientId) || !string.IsNullOrWhiteSpace(ClientSecret)))
        {
            yield return new ValidationResult("Choose either Fleet API key or homeowner OAuth credentials, not both.", [nameof(AuthToken), nameof(ClientId), nameof(ClientSecret)]);
        }
    }
}

public sealed class ModbusOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("MODBUS_PROXY_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = string.Empty;

    [Secret]
    [ConfigurationKeyName("MODBUS_PROXY_CLIENT_SECRET")]
    public string? ClientSecret { get; set; }
}
