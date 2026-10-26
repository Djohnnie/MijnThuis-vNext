using MijnThuis.Integrations.Power;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class PowerMenu : IntegrationMenu<IPowerService>
{
    public override string Title => "Power meter (HomeWizard P1)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get overview", (s, ct) => s.GetOverviewAsync(ct))
    ];
}
