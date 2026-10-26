using MijnThuis.Integrations.Airconditioning;
using MijnThuis.Integrations.CLI.Rendering;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class AirconditioningMenu : IntegrationMenu<IAirconditioningService>
{
    public override string Title => "Airconditioning (Gree)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get overview", (s, ct) => s.GetOverviewAsync(ct)),
        Command("Turn on", (s, ct) => s.TurnOnAsync(ct)),
        Command("Turn off", (s, ct) => s.TurnOffAsync(ct)),
        Command("Set target temperature", () => Prompts.Int("Target temperature (°C)?", 21, 16, 30), (s, t, ct) => s.SetTargetTemperatureAsync(t, ct)),
        Command("Increase target temperature", (s, ct) => s.IncreaseTargetTemperatureAsync(ct)),
        Command("Decrease target temperature", (s, ct) => s.DecreaseTargetTemperatureAsync(ct))
    ];
}
