using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.Heating;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class HeatingMenu : IntegrationMenu<IHeatingService>
{
    public override string Title => "Heating (Remeha)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get overview", (s, ct) => s.GetOverviewAsync(ct)),
        Command("Set manual heating", () => Prompts.Decimal("Temperature (°C)?", 20M, 5M, 30M), (s, t, ct) => s.SetManualHeatingAsync(t, ct)),
        Command("Set temporary override", () => Prompts.Decimal("Temperature (°C)?", 20M, 5M, 30M), (s, t, ct) => s.SetTemporaryOverrideHeatingAsync(t, ct)),
        Command("Set scheduled heating", () => Prompts.Int("Heating program id?", 1, 1, 3), (s, id, ct) => s.SetScheduledHeatingAsync(id, ct)),
        Command("Set anti-frost heating", (s, ct) => s.SetAntiFrostHeatingAsync(ct))
    ];
}
