using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.SmartLock;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class SmartLockMenu : IntegrationMenu<ISmartLockService>
{
    public override string Title => "Smart lock (Nuki)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get overview", (s, ct) => s.GetOverviewAsync(ct)),
        Query("Get activity log", () => Prompts.Int("Number of entries?", 10, 1, 100), (s, limit, ct) => s.GetActivityLogAsync(limit, ct)),
        Command("Unlock", (s, ct) => s.UnlockAsync(ct))
    ];
}
