using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Samsung;

public sealed class SamsungOptions
{
    [Required]
    [RegularExpression(ValidationPatterns.MacAddress, ErrorMessage = ValidationPatterns.MacAddressMessage)]
    [ConfigurationKeyName("THE_FRAME_MAC_ADDRESS")]
    public string MacAddress { get; set; } = string.Empty;

    [Required]
    [IpAddress]
    [ConfigurationKeyName("THE_FRAME_IP_ADDRESS")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Name shown on the TV when it asks to allow the remote control connection.
    /// </summary>
    [Required]
    [ConfigurationKeyName("THE_FRAME_APPNAME")]
    public string AppName { get; set; } = "MijnThuis";

    /// <summary>
    /// Optional remote control token. When empty, a token is requested from the TV (which needs to be accepted on the TV once).
    /// </summary>
    [Secret]
    [ConfigurationKeyName("THE_FRAME_TOKEN")]
    public string? Token { get; set; }

    /// <summary>
    /// Time to wait after sending the Wake-on-LAN packet before the TV accepts remote control commands.
    /// </summary>
    [ConfigurationKeyName("THE_FRAME_BOOT_DELAY")]
    public TimeSpan BootDelay { get; set; } = TimeSpan.FromSeconds(10);
}
