using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Power;

public sealed class PowerOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("POWER_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = string.Empty;
}

public sealed class EnergyPricesOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("ENERGY_PRICES_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = "https://web-api.tp.entsoe.eu/api";

    [Required]
    [Secret]
    [ConfigurationKeyName("ENERGY_PRICES_API_KEY")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// EIC code of the bidding zone. Defaults to Belgium.
    /// </summary>
    [Required]
    [ConfigurationKeyName("ENERGY_PRICES_AREA_CODE")]
    public string AreaCode { get; set; } = "10YBE----------2";
}

public sealed class PowerSwitchOptions
{
    [Url]
    [ConfigurationKeyName("TV_POWER_SWITCH_BASE_ADDRESS")]
    public string? TvBaseAddress { get; set; }

    [Url]
    [ConfigurationKeyName("BUREAU_POWER_SWITCH_BASE_ADDRESS")]
    public string? BureauBaseAddress { get; set; }

    [Url]
    [ConfigurationKeyName("VIJVER_POWER_SWITCH_BASE_ADDRESS")]
    public string? VijverBaseAddress { get; set; }
}

public sealed class WakeOnLanOptions
{
    [Required]
    [RegularExpression(ValidationPatterns.MacAddress, ErrorMessage = ValidationPatterns.MacAddressMessage)]
    [ConfigurationKeyName("WAKE_ON_LAN_MAC_ADDRESS")]
    public string MacAddress { get; set; } = string.Empty;

    [Required]
    [IpAddress]
    [ConfigurationKeyName("WAKE_ON_LAN_IP_ADDRESS")]
    public string IpAddress { get; set; } = string.Empty;
}
