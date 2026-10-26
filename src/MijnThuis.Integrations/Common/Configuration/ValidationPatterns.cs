namespace MijnThuis.Integrations.Common.Configuration;

internal static class ValidationPatterns
{
    public const string MacAddress = "^([0-9A-Fa-f]{2}[:-]?){5}[0-9A-Fa-f]{2}$";
    public const string MacAddressMessage = "{0} must be a valid MAC address (e.g. AA:BB:CC:DD:EE:FF).";
}
