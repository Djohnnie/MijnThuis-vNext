using MijnThuis.Integrations.Car;
using MijnThuis.Integrations.CLI.Rendering;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class CarMenu : IntegrationMenu<ICarService>
{
    public override string Title => "Car (Tessie)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get overview", (s, ct) => s.GetOverviewAsync(ct)),
        Query("Get battery health", (s, ct) => s.GetBatteryHealthAsync(ct)),
        Query("Get location", (s, ct) => s.GetLocationAsync(ct)),
        Query("Get charges", (s, ct) => s.GetChargesAsync(ct)),
        Query("Get drives", (s, ct) => s.GetDrivesAsync(ct)),
        Command("Lock", (s, ct) => s.LockAsync(ct)),
        Command("Unlock", (s, ct) => s.UnlockAsync(ct)),
        Command("Honk", (s, ct) => s.HonkAsync(ct)),
        Command("Fart", (s, ct) => s.FartAsync(ct)),
        Command("Preheat", (s, ct) => s.PreheatAsync(ct)),
        Command("Start charging", () => Prompts.Int("Charging amps?", 16, 1, 32), (s, amps, ct) => s.StartChargingAsync(amps, ct)),
        Command("Stop charging", (s, ct) => s.StopChargingAsync(ct)),
        Command("Set cabin overheat protection",
            () => (Enable: Prompts.Confirm("Enable cabin overheat protection?"), FanOnly: Prompts.Confirm("Fan only (no A/C)?", false)),
            (s, input, ct) => s.SetCabinOverheatProtectionAsync(input.Enable, input.FanOnly, ct)),
        Command("Set cabin overheat protection temperature",
            () => Prompts.Choice("Temperature (°C)?", [30, 35, 40]),
            (s, temperature, ct) => s.SetCabinOverheatProtectionTemperatureAsync(temperature, ct))
    ];
}
