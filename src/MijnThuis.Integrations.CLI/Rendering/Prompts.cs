using System.Globalization;
using Spectre.Console;

namespace MijnThuis.Integrations.CLI.Rendering;

/// <summary>
/// Input prompts for action parameters.
/// </summary>
internal static class Prompts
{
    public static string Text(string title, string? defaultValue = null)
    {
        var prompt = new TextPrompt<string>(title);
        if (defaultValue is not null)
        {
            prompt.DefaultValue(defaultValue);
        }

        return AnsiConsole.Prompt(prompt).Trim();
    }

    public static int Int(string title, int defaultValue, int min = int.MinValue, int max = int.MaxValue)
    {
        return AnsiConsole.Prompt(new TextPrompt<int>(title)
            .DefaultValue(defaultValue)
            .Validate(value => value >= min && value <= max
                ? ValidationResult.Success()
                : ValidationResult.Error($"[red]Enter a value between {min} and {max}.[/]")));
    }

    /// <summary>
    /// Accepts both "21.5" and "21,5".
    /// </summary>
    public static decimal Decimal(string title, decimal defaultValue, decimal min = decimal.MinValue, decimal max = decimal.MaxValue)
    {
        var text = AnsiConsole.Prompt(new TextPrompt<string>(title)
            .DefaultValue(defaultValue.ToString(CultureInfo.InvariantCulture))
            .Validate(value => TryParseDecimal(value, out var parsed) && parsed >= min && parsed <= max
                ? ValidationResult.Success()
                : ValidationResult.Error($"[red]Enter a number between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}.[/]")));

        TryParseDecimal(text, out var result);
        return result;
    }

    public static DateTime Date(string title, DateTime defaultValue)
    {
        const string format = "yyyy-MM-dd";

        var text = AnsiConsole.Prompt(new TextPrompt<string>($"{title} [grey]({format})[/]")
            .DefaultValue(defaultValue.ToString(format, CultureInfo.InvariantCulture))
            .Validate(value => DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? ValidationResult.Success()
                : ValidationResult.Error($"[red]Enter a date as {format}.[/]")));

        return DateTime.ParseExact(text, format, CultureInfo.InvariantCulture);
    }

    public static bool Confirm(string title, bool defaultValue = true) => AnsiConsole.Confirm(title, defaultValue);

    public static T Choice<T>(string title, IEnumerable<T> choices, Func<T, string>? display = null)
        where T : notnull
    {
        var prompt = new SelectionPrompt<T>().Title(title).AddChoices(choices);
        if (display is not null)
        {
            prompt.UseConverter(display);
        }

        return AnsiConsole.Prompt(prompt);
    }

    public static T Enum<T>(string title) where T : struct, Enum
        => Choice(title, System.Enum.GetValues<T>());

    private static bool TryParseDecimal(string value, out decimal result)
        => decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
}
