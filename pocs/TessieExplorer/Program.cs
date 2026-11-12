using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using TessieExplorer;

ExplorerConfiguration settings;
try
{
    settings = ExplorerConfiguration.Load(Directory.GetCurrentDirectory());
}
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or FormatException)
{
    AnsiConsole.MarkupLine("[red]Could not read application settings. Check the appsettings JSON files.[/]");
    return;
}

var token = settings.Token;
var vin = settings.Vin;
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
