using MijnThuis.Integrations.Samsung;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class SamsungMenu : IntegrationMenu<ISamsungService>
{
    public override string Title => "TV (Samsung The Frame)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Is the TV on?", (s, ct) => s.IsTheFrameOnAsync(ct)),
        Command("Turn on", (s, ct) => s.TurnOnTheFrameAsync(ct)),
        Command("Turn off", (s, ct) => s.TurnOffTheFrameAsync(ct))
    ];
}
