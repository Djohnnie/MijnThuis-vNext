using MijnThuis.Integrations.Sauna;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class SaunaMenu : IntegrationMenu<ISaunaService>
{
    public override string Title => "Sauna";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get state", (s, ct) => s.GetStateAsync(ct)),
        Query("Get inside temperature", (s, ct) => s.GetInsideTemperatureAsync(ct)),
        Query("Get outside temperature", (s, ct) => s.GetOutsideTemperatureAsync(ct)),
        Query("Get power usage", (s, ct) => s.GetPowerUsageAsync(ct)),
        Query("Get active session", (s, ct) => s.GetActiveSessionAsync(ct)),
        Command("Start sauna", (s, ct) => s.StartSaunaAsync(ct)),
        Command("Start infrared", (s, ct) => s.StartInfraredAsync(ct)),
        Command("Stop active session", async (s, ct) =>
        {
            var sessionId = await s.GetActiveSessionAsync(ct) ?? throw new InvalidOperationException("There is no active session.");
            await s.StopSessionAsync(sessionId, ct);
        })
    ];
}
