namespace MijnThuis.Integrations.Common.Configuration;

/// <summary>
/// Marks an options property as sensitive (token, password, API key, ...) so tooling can mask its value.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SecretAttribute : Attribute;
