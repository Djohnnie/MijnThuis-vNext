using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.Forecast;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class ForecastMenu : IntegrationMenu<IForecastService>
{
    public override string Title => "Solar forecast (Forecast.Solar)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get solar forecast estimate", PromptPanels, (s, panels, ct) => s.GetSolarForecastEstimateAsync(panels, ct))
    ];

    private static SolarPanelArray PromptPanels() => new(
        Latitude: Prompts.Decimal("Latitude?", 51.05M, -90M, 90M),
        Longitude: Prompts.Decimal("Longitude?", 3.72M, -180M, 180M),
        Declination: Prompts.Decimal("Declination (0 = horizontal, 90 = vertical)?", 35M, 0M, 90M),
        Azimuth: Prompts.Decimal("Azimuth (-180 = north, -90 = east, 0 = south, 90 = west)?", 0M, -180M, 180M),
        Power: Prompts.Decimal("Installed power (kWp)?", 5M, 0.001M, 1000M),
        Damping: (byte)Prompts.Int("Damping (0-1)?", 0, 0, 1));
}
