using MijnThuis.Integrations.CLI.Rendering;
using MijnThuis.Integrations.Solar;

namespace MijnThuis.Integrations.CLI.Menus.Integrations;

internal sealed class ModbusMenu : IntegrationMenu<IModbusService>
{
    public override string Title => "Solar inverter (Modbus proxy)";

    public override IEnumerable<MenuAction> GetActions() =>
    [
        Query("Get bulk overview", (s, ct) => s.GetBulkOverviewAsync(ct)),
        Query("Get overview", (s, ct) => s.GetOverviewAsync(ct)),
        Query("Get battery level", (s, ct) => s.GetBatteryLevelAsync(ct)),
        Query("Has export limitation?", (s, ct) => s.HasExportLimitationAsync(ct)),
        Command("Start charging battery",
            () => (Minutes: Prompts.Int("Duration (minutes)?", 60, 1, 24 * 60), Power: Prompts.Int("Power (W)?", 1000, 1, 10000)),
            (s, input, ct) => s.StartChargingBatteryAsync(TimeSpan.FromMinutes(input.Minutes), input.Power, ct)),
        Command("Stop charging battery", (s, ct) => s.StopChargingBatteryAsync(ct)),
        Command("Set export limitation", () => (float)Prompts.Decimal("Power limit (W)?", 0M, 0M, 100000M), (s, limit, ct) => s.SetExportLimitationAsync(limit, ct)),
        Command("Reset export limitation", (s, ct) => s.ResetExportLimitationAsync(ct))
    ];
}
