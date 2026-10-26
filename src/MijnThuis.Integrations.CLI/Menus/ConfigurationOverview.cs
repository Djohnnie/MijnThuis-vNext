using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common.Configuration;
using Spectre.Console;

namespace MijnThuis.Integrations.CLI.Menus;

/// <summary>
/// Shows every configuration key used by the integrations, whether it is set, and whether the options validate.
/// Secret values are masked.
/// </summary>
internal sealed class ConfigurationOverview(IServiceProvider serviceProvider, IConfiguration configuration)
{
    public void Render()
    {
        var optionTypes = typeof(DependencyInjection).Assembly.GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Options", StringComparison.Ordinal))
            .Where(t => t.GetProperties().Any(p => p.GetCustomAttribute<ConfigurationKeyNameAttribute>() is not null))
            .OrderBy(t => t.Namespace).ThenBy(t => t.Name);

        var summary = new Table().RoundedBorder()
            .AddColumn("[bold]Options[/]")
            .AddColumn("[bold]Key[/]")
            .AddColumn("[bold]Value[/]")
            .AddColumn("[bold]Status[/]");

        foreach (var type in optionTypes)
        {
            var failures = Validate(type);
            var defaults = Activator.CreateInstance(type);
            var first = true;

            foreach (var property in type.GetProperties())
            {
                var key = property.GetCustomAttribute<ConfigurationKeyNameAttribute>()?.Name;
                if (key is null)
                {
                    continue;
                }

                var value = configuration[key];
                var isSecret = property.GetCustomAttribute<SecretAttribute>() is not null;
                var isRequired = property.GetCustomAttribute<RequiredAttribute>() is not null;
                var defaultValue = property.GetValue(defaults)?.ToString();
                var hasError = failures.Any(f => Regex.IsMatch(f, $@"\b({property.Name}|{key})\b"));

                var (display, status) = (string.IsNullOrEmpty(value), string.IsNullOrEmpty(defaultValue)) switch
                {
                    (false, _) => (isSecret ? Mask(value!) : Markup.Escape(value!), hasError ? "[red]invalid[/]" : "[green]set[/]"),
                    (true, false) => ($"[grey]{Markup.Escape(defaultValue!)}[/]", hasError ? "[red]invalid[/]" : "[blue]default[/]"),
                    (true, true) => ("[grey]-[/]", isRequired ? "[red]missing[/]" : "[grey]optional[/]")
                };

                summary.AddRow(first ? $"[bold]{Markup.Escape(type.Name.Replace("Options", string.Empty))}[/]" : string.Empty, Markup.Escape(key), display, status);
                first = false;
            }

            foreach (var failure in failures)
            {
                summary.AddRow(string.Empty, $"[red]{Markup.Escape(failure)}[/]", string.Empty, string.Empty);
            }

            summary.AddEmptyRow();
        }

        AnsiConsole.Write(summary);
    }

    private IReadOnlyList<string> Validate(Type optionsType)
    {
        try
        {
            var options = serviceProvider.GetRequiredService(typeof(IOptions<>).MakeGenericType(optionsType));
            _ = options.GetType().GetProperty(nameof(IOptions<object>.Value))!.GetValue(options);
            return [];
        }
        catch (TargetInvocationException ex) when (ex.InnerException is OptionsValidationException validation)
        {
            return validation.Failures.ToList();
        }
    }

    private static string Mask(string value)
        => value.Length <= 4 ? "[grey]****[/]" : $"{Markup.Escape(value[..2])}[grey]{new string('*', Math.Min(value.Length - 4, 12))}[/]{Markup.Escape(value[^2..])}";
}
