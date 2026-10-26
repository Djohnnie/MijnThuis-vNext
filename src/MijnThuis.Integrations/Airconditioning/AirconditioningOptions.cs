using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Airconditioning;

public sealed class AirconditioningOptions
{
    [Required]
    [IpAddress]
    [ConfigurationKeyName("AIRCONDITIONING_IP_ADDRESS")]
    public string IpAddress { get; set; } = string.Empty;

    [Required]
    [RegularExpression(ValidationPatterns.MacAddress, ErrorMessage = ValidationPatterns.MacAddressMessage)]
    [ConfigurationKeyName("AIRCONDITIONING_MAC_ADDRESS")]
    public string MacAddress { get; set; } = string.Empty;
}
