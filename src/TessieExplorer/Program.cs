using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using TessieExplorer;

string? token = Environment.GetEnvironmentVariable("TESSIE_TOKEN");
string? vin = Environment.GetEnvironmentVariable("TESSIE_VIN");
if (File.Exists("appsettings.Development.json"))
{
    try
    {
        using var settings = JsonDocument.Parse(File.ReadAllText("appsettings.Development.json"));
        if (settings.RootElement.TryGetProperty("Tessie", out var tessie))
        {
            token ??= tessie.TryGetProperty("Token", out var t) ? t.GetString() : null;
            vin ??= tessie.TryGetProperty("Vin", out var v) ? v.GetString() : null;
        }
    }
    catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
    {
        AnsiConsole.MarkupLine("[red]Could not read appsettings.Development.json.[/]");
        return;
    }
}

if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(vin) ||
    !Regex.IsMatch(vin, "^[A-HJ-NPR-Z0-9]{17}$", RegexOptions.IgnoreCase))
{
    AnsiConsole.MarkupLine("[red]Configure a Tessie token and a valid 17-character VIN first. See README.[/]");
    return;
}

if (!AnsiConsole.Profile.Capabilities.Interactive)
{
    AnsiConsole.MarkupLine("[red]An interactive terminal is required for the menu.[/]");
    return;
}

using var http = new HttpClient { BaseAddress = new Uri("https://api.tessie.com/") };
var client = new TessieClient(http, token, vin.ToUpperInvariant());
var presenter = new VehiclePresenter(AnsiConsole.Console);
while (true)
{
    var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
        .Title("Select vehicle data")
        .AddChoices("All vehicle data", "Vehicle state", "Battery health", "Location", "Charge history", "Drive history", "Exit"));
    if (choice == "Exit")
    {
        break;
    }

    foreach (var section in Enum.GetValues<VehicleSection>())
    {
        if (choice != "All vehicle data" && choice != VehiclePresenter.Title(section))
        {
            continue;
        }

        try
        {
            var data = await client.GetAsync(section);
            presenter.Display(section, data, vin);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(VehiclePresenter.Title(section))}: request failed ({Markup.Escape(ex.Message)}).[/]");
        }
    }
}
