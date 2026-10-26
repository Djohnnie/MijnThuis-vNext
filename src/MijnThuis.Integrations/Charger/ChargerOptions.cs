using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace MijnThuis.Integrations.Charger;

public sealed class ChargerOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("CHARGER_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = string.Empty;
}
