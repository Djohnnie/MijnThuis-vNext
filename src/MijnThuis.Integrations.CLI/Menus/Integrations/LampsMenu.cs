using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.Lamps;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class LampsMenu : IntegrationMenu<ILampsService>
{
    public override string Title => "Lamps (LIFX)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get lamps", PromptSelector, (s, selector, ct) => s.GetLampsAsync(selector, ct)),
        Query("Turn on", PromptSelector, (s, selector, ct) => s.SetPowerAsync(true, selector, ct)),
        Query("Turn off", PromptSelector, (s, selector, ct) => s.SetPowerAsync(false, selector, ct)),
        Query("Set brightness",
            () => (Selector: PromptSelector(), Brightness: Prompts.Int("Brightness (%)?", 50, 0, 100)),
            (s, input, ct) => s.SetBrightnessAsync(input.Brightness / 100M, input.Selector, ct))
    ];

    private static string PromptSelector()
        => Prompts.Text("Selector? [grey](all, id:..., label:..., group:...)[/]", ILampsService.AllLamps);
}
