using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.Power;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class PowerSwitchesMenu : IntegrationMenu<IShellyService>
{
    public override string Title => "Power switches (Shelly)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get power switch", () => Prompts.Enum<PowerSwitch>("Which power switch?"), (s, powerSwitch, ct) => s.GetPowerSwitchAsync(powerSwitch, ct)),
        Query("Get all power switches", async (s, ct) =>
        {
            var results = new List<object>();
            foreach (var powerSwitch in Enum.GetValues<PowerSwitch>())
            {
                try
                {
                    results.Add(await s.GetPowerSwitchAsync(powerSwitch, ct));
                }
                catch (Common.Exceptions.IntegrationException ex)
                {
                    results.Add(new { Switch = powerSwitch, Error = ex.Message });
                }
            }

            return results;
        }),
        Command("Turn on", () => Prompts.Enum<PowerSwitch>("Which power switch?"), (s, powerSwitch, ct) => s.SetPowerSwitchAsync(powerSwitch, true, ct)),
        Command("Turn off", () => Prompts.Enum<PowerSwitch>("Which power switch?"), (s, powerSwitch, ct) => s.SetPowerSwitchAsync(powerSwitch, false, ct))
    ];
}
