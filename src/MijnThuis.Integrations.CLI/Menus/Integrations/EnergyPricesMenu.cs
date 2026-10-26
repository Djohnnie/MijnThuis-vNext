using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.Power;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class EnergyPricesMenu : IntegrationMenu<IEnergyPricesService>
{
    public override string Title => "Energy prices (ENTSO-E)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get day-ahead prices for date", () => Prompts.Date("Date?", DateTime.Today), (s, date, ct) => s.GetEnergyPricesForDateAsync(date, ct))
    ];
}
