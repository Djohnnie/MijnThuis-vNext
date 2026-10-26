using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MijnThuis.Integrations;
using MijnThuis.Integrations.CLI.Menus;
using MijnThuis.Integrations.CLI.Menus.Integrations;
using MijnThuis.Integrations.CLI.Rendering;

Console.OutputEncoding = Encoding.UTF8;

// Configuration comes from environment variables (Properties/launchSettings.json) and, in Development, user secrets.
var builder = Host.CreateApplicationBuilder(args);

// Errors are rendered by the CLI itself, so keep the console free of log output.
builder.Logging.ClearProviders();

builder.Services.AddIntegrations(builder.Configuration);

builder.Services.AddSingleton<ActionRunner>();
builder.Services.AddSingleton<ConfigurationOverview>();
builder.Services.AddSingleton<MainMenu>();
builder.Services.AddSingleton<IIntegrationMenu, AirconditioningMenu>();
builder.Services.AddSingleton<IIntegrationMenu, CarMenu>();
builder.Services.AddSingleton<IIntegrationMenu, ChargerMenu>();
builder.Services.AddSingleton<IIntegrationMenu, EnergyPricesMenu>();
builder.Services.AddSingleton<IIntegrationMenu, ForecastMenu>();
builder.Services.AddSingleton<IIntegrationMenu, HeatingMenu>();
builder.Services.AddSingleton<IIntegrationMenu, LampsMenu>();
builder.Services.AddSingleton<IIntegrationMenu, ModbusMenu>();
builder.Services.AddSingleton<IIntegrationMenu, PowerMenu>();
builder.Services.AddSingleton<IIntegrationMenu, PowerSwitchesMenu>();
builder.Services.AddSingleton<IIntegrationMenu, SamsungMenu>();
builder.Services.AddSingleton<IIntegrationMenu, SaunaMenu>();
builder.Services.AddSingleton<IIntegrationMenu, SmartLockMenu>();
builder.Services.AddSingleton<IIntegrationMenu, SolarMenu>();
builder.Services.AddSingleton<IIntegrationMenu, WakeOnLanMenu>();

using var host = builder.Build();

// "--config" prints the configuration overview without the interactive menu (e.g. to check settings in CI or a script).
if (args.Contains("--config", StringComparer.OrdinalIgnoreCase))
{
    host.Services.GetRequiredService<ConfigurationOverview>().Render();
    return;
}

var actionRunner = host.Services.GetRequiredService<ActionRunner>();

// Ctrl+C cancels the running integration call; when nothing is running, it exits the application.
Console.CancelKeyPress += (_, e) => e.Cancel = actionRunner.CancelCurrent();

await host.Services.GetRequiredService<MainMenu>().RunAsync();
