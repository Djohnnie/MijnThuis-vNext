using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.Lamps;

public sealed class LampsOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("LAMPS_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = "https://api.lifx.com/v1/";

    [Required]
    [Secret]
    [ConfigurationKeyName("LAMPS_API_AUTH_TOKEN")]
    public string AuthToken { get; set; } = string.Empty;
}
