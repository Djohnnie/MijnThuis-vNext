using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Heating;

public sealed class HeatingOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("HEATING_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = string.Empty;

    [Required]
    [Secret]
    [ConfigurationKeyName("HEATING_API_SUBSCRIPTION_KEY")]
    public string SubscriptionKey { get; set; } = string.Empty;

    [Required]
    [Url]
    [ConfigurationKeyName("HEATING_API_AUTH_BASE_ADDRESS")]
    public string AuthBaseAddress { get; set; } = string.Empty;

    [Required]
    [ConfigurationKeyName("HEATING_API_AUTH_SCOPE")]
    public string AuthScope { get; set; } = string.Empty;

    [Required]
    [ConfigurationKeyName("HEATING_API_AUTH_REDIRECT_URI")]
    public string AuthRedirectUri { get; set; } = string.Empty;

    [Required]
    [ConfigurationKeyName("HEATING_API_AUTH_CLIENT_ID")]
    public string AuthClientId { get; set; } = string.Empty;

    [Required]
    [ConfigurationKeyName("HEATING_API_AUTH_USERNAME")]
    public string AuthUsername { get; set; } = string.Empty;

    [Required]
    [Secret]
    [ConfigurationKeyName("HEATING_API_AUTH_PASSWORD")]
    public string AuthPassword { get; set; } = string.Empty;
}
