using MijnThuis.Integrations.Power;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class WakeOnLanMenu : IntegrationMenu<IWakeOnLanService>
{
    public override string Title => "Wake-on-LAN";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Command("Wake computer", (s, ct) => s.WakeAsync(ct))
    ];
}
