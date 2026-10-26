using Microsoft.Extensions.Configuration;
using MijnThuis.Integrations.Charger;
using MijnThuis.Integrations.CLI.Rendering;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class ChargerMenu(IConfiguration configuration) : IntegrationMenu<IChargerService>
{
    private const string OtherCharger = "Other...";

    public override string Title => "Charger";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get charger overview", PromptChargerId, (s, id, ct) => s.GetChargerOverviewAsync(id, ct))
    ];

    // Known charger ids (CHARGER_1_ID, CHARGER_2_ID) are offered as choices; any other id can still be entered.
    private string PromptChargerId()
    {
        var knownIds = new[] { configuration["CHARGER_1_ID"], configuration["CHARGER_2_ID"] }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .ToList();

        if (knownIds.Count == 0)
        {
            return Prompts.Text("Charger id?");
        }

        var choice = Prompts.Choice("Which charger?", knownIds.Append(OtherCharger));
        return choice == OtherCharger ? Prompts.Text("Charger id?") : choice;
    }
}
