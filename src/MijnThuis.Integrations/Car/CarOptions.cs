using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Car;

public sealed class CarOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("CAR_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = "https://api.tessie.com/";

    [Required]
    [Secret]
    [ConfigurationKeyName("CAR_API_AUTH_TOKEN")]
    public string AuthToken { get; set; } = string.Empty;

    [Required]
    [ConfigurationKeyName("CAR_VIN_NUMBER")]
    public string VinNumber { get; set; } = string.Empty;
}
