using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.Solar;
using Spectre.Console;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class SolarMenu(ISolarAuthorizationService authorization) : IntegrationMenu<ISolarService>
{
    public override string Title => "Solar (SolarEdge)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        new MenuAction("Connect SolarEdge (OAuth)", () =>
        {
            AnsiConsole.MarkupLine("[grey]Open this link, sign in, and approve SITE_DATA and DEVICE_DATA access. Then paste the complete redirect URL.[/]");
            AnsiConsole.WriteLine(authorization.GetAuthorizationUri().AbsoluteUri);
            var callback = Prompts.Text("Redirect URL?");
            return async (_, ct) =>
            {
                await authorization.AuthorizeAsync(callback, ct);
                return CommandCompleted.Instance;
            };
        }),
        Query("Get overview", (s, ct) => s.GetOverviewAsync(ct)),
        Query("Get battery level", (s, ct) => s.GetBatteryLevelAsync(ct)),
        Query("Get energy produced (today and this month)", (s, ct) => s.GetEnergyAsync(ct)),
        Query("Get energy today", (s, ct) => s.GetEnergyTodayAsync(ct)),
        Query("Get energy this month", (s, ct) => s.GetEnergyThisMonthAsync(ct)),
        Query("Get energy overview for date", PromptDate, (s, date, ct) => s.GetEnergyOverviewAsync(date, ct)),
        Query("Get power overview for date", PromptDate, (s, date, ct) => s.GetPowerOverviewAsync(date, ct)),
        Query("Get battery overview for date", PromptDate, (s, date, ct) => s.GetBatteryOverviewAsync(date, ct)),
        Query("Get storage data", () => Prompts.Enum<StorageDataRange>("Range?"), (s, range, ct) => s.GetStorageDataAsync(range, ct))
    ];

    private static DateTime PromptDate() => Prompts.Date("Date?", DateTime.Today);
}
