using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MijnThuis.Integrations.Common.Configuration;

internal static class OptionsRegistrationExtensions
{
    /// <summary>
    /// Binds integration options from the root configuration. Option properties are mapped to the
    /// (environment variable style) configuration keys with <see cref="ConfigurationKeyNameAttribute"/>.
    /// Empty values are ignored, so placeholders (e.g. in launchSettings.json) don't override defaults.
    /// Validation is performed lazily on first use, so a missing setting for one integration never
    /// prevents the other integrations from working.
    /// </summary>
    public static OptionsBuilder<TOptions> AddIntegrationOptions<TOptions>(this IServiceCollection services, IConfiguration configuration)
        where TOptions : class
    {
        services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(new ConfigurationChangeTokenSource<TOptions>(configuration));

        return services
            .AddOptions<TOptions>()
            .Configure(options => Bind(options, configuration))
            .ValidateDataAnnotations();
    }

    private static void Bind<TOptions>(TOptions options, IConfiguration configuration)
        where TOptions : class
    {
        var errors = new List<string>();

        foreach (var property in typeof(TOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = property.GetCustomAttribute<ConfigurationKeyNameAttribute>()?.Name;
            if (key is null || !property.CanWrite)
            {
                continue;
            }

            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            try
            {
                property.SetValue(options, TypeDescriptor.GetConverter(targetType).ConvertFromString(null, CultureInfo.InvariantCulture, value.Trim()));
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException || ex.InnerException is FormatException)
            {
                errors.Add($"'{key}' has an invalid value: expected a {targetType.Name}.");
            }
        }

        if (errors.Count > 0)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(TOptions), errors);
        }
    }
}
