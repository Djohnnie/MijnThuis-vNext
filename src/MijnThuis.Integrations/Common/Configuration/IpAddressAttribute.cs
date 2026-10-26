using System.ComponentModel.DataAnnotations;
using System.Net;

namespace MijnThuis.Integrations.Common.Configuration;

/// <summary>
/// Validates that a string option contains a valid IPv4 or IPv6 address.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IpAddressAttribute() : ValidationAttribute("{0} must be a valid IP address.")
{
    public override bool IsValid(object? value)
        => value is null || (value is string text && (text.Length == 0 || IPAddress.TryParse(text, out _)));
}
