using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Solar.SolarEdge;

namespace MijnThuis.Integrations.Solar;

public interface ISolarService
{
    Task<SolarOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<BatteryLevel> GetBatteryLevelAsync(CancellationToken cancellationToken = default);
    Task<EnergyProduced> GetEnergyAsync(CancellationToken cancellationToken = default);
    Task<EnergyOverviewResponse> GetEnergyOverviewAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<PowerOverviewResponse> GetPowerOverviewAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<BatteryOverviewResponse> GetBatteryOverviewAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<EnergyOverview> GetEnergyTodayAsync(CancellationToken cancellationToken = default);
    Task<EnergyOverview> GetEnergyThisMonthAsync(CancellationToken cancellationToken = default);
    Task<StorageData> GetStorageDataAsync(StorageDataRange range, CancellationToken cancellationToken = default);
}

/// <summary>
/// SolarEdge V2. Power is returned in kW and energy in Wh. Site-local dates are retained in the
/// existing chart models; requests use offset timestamps and respect V2's 12-hour telemetry limit.
/// Live power flow and per-destination charts require an Advanced Monitoring subscription.
/// </summary>
internal sealed class SolarService(
    HttpClient httpClient, IOptions<SolarOptions> options, TimeProvider timeProvider,
    IMemoryCache cache, ILogger<SolarService> logger) : HttpIntegrationServiceBase(httpClient, logger), ISolarService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    protected override string IntegrationName => IntegrationNames.Solar;
    protected override JsonSerializerOptions SerializerOptions => JsonOptions;
    private string Site => $"sites/{Uri.EscapeDataString(options.Value.SiteId)}";

    public Task<SolarOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("Get overview", async ct =>
        {
            var result = await GetJsonAsync<PowerflowResponse>("Get overview", $"{Site}/power-flow", ct);
            if (result.UpdatedAt is null || result.Pv is null || result.Load is null || result.Storage is null || result.Grid is null)
            {
                throw ResponseError("Get overview", "The power-flow response did not contain a timestamp and all four components.");
            }

            decimal Power(PowerflowElement element) => element.Active
                ? ConvertUnit(Required(element.Power, "power"), result.Unit, energy: false) : 0;

            decimal SignedPower(PowerflowElement element, string positive, string negative)
            {
                var direction = element.Active ? Direction(element.Status, positive, negative) : 0;
                return direction == 0 ? 0 : direction * Power(element);
            }

            return new SolarOverview
            {
                CurrentSolarPower = Power(result.Pv),
                CurrentConsumptionPower = Power(result.Load),
                CurrentBatteryPower = SignedPower(result.Storage, "CHARGE", "DISCHARGE"),
                CurrentGridPower = SignedPower(result.Grid, "IMPORT", "EXPORT"),
                BatteryLevel = Required(result.Storage.ChargeLevel, "storage charge level"),
                UpdatedAt = result.UpdatedAt,
                RefreshRate = result.RefreshRate
            };
        }, cancellationToken);

    public Task<BatteryLevel> GetBatteryLevelAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("Get battery level", async ct =>
        {
            var now = timeProvider.GetUtcNow();
            var overview = await ReadStorageAsync("Get battery level", now.AddHours(-1), now, ct);
            var battery = FirstBattery(overview);
            var sample = battery.Telemetries.LastOrDefault(x => x.Level.HasValue)
                ?? throw ResponseError("Get battery level", "No state-of-energy samples were reported in the last hour.");
            if (battery.Nameplate <= 0 || sample.EnergyAvailable is null)
            {
                throw ResponseError("Get battery level", "Battery health is unavailable: usable capacity requires matching remaining-energy and non-zero state-of-energy samples, plus a positive nameplate capacity.");
            }

            return new BatteryLevel
            {
                Level = sample.Level!.Value,
                Health = sample.EnergyAvailable.Value / battery.Nameplate * 100,
                MaxEnergy = sample.EnergyAvailable.Value
            };
        }, cancellationToken);

    public Task<EnergyProduced> GetEnergyAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("Get energy", async ct =>
        {
            var zone = await GetSiteZoneAsync(ct);
            var today = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).Date;
            var start = new DateTime(today.Year, today.Month, 1);
            var values = await GetProductionAsync("Get energy", AtSite(start, zone), timeProvider.GetUtcNow(), "DAY", ct);
            return new EnergyProduced(
                values.Values.Where(v => TimeZoneInfo.ConvertTime(v.Timestamp, zone).Date == today)
                    .Sum(v => ConvertUnit(v.Value, values.Unit, energy: true) ?? 0),
                values.Values.Sum(v => ConvertUnit(v.Value, values.Unit, energy: true) ?? 0));
        }, cancellationToken);

    public Task<EnergyOverviewResponse> GetEnergyOverviewAsync(DateTime date, CancellationToken cancellationToken = default)
        => ExecuteAsync("Get energy overview", async ct =>
        {
            var zone = await GetSiteZoneAsync(ct);
            var (production, consumption) = await ReadDestinationsAsync("Get energy overview", "energy", date, zone, ct);
            var timestamps = production.Keys.Union(consumption.Keys).Order().ToList();
            return new EnergyOverviewResponse
            {
                Summary = new EnergySummary { Production = production.Values.Sum(v => v.Total ?? 0) },
                Chart = new EnergyChart
                {
                    Measurements = timestamps.Select(timestamp => new EnergyMeasurement
                    {
                        MeasurementTime = TimeZoneInfo.ConvertTime(timestamp, zone).DateTime,
                        Production = production.GetValueOrDefault(timestamp)?.Total,
                        Consumption = consumption.GetValueOrDefault(timestamp)?.Total,
                        Export = production.GetValueOrDefault(timestamp)?.ToGrid,
                        Import = consumption.GetValueOrDefault(timestamp)?.FromGrid,
                        ProductionDistribution = Production(production.GetValueOrDefault(timestamp)),
                        ConsumptionDistribution = Consumption(consumption.GetValueOrDefault(timestamp))
                    }).ToList()
                }
            };
        }, cancellationToken);

    public Task<PowerOverviewResponse> GetPowerOverviewAsync(DateTime date, CancellationToken cancellationToken = default)
        => ExecuteAsync("Get power overview", async ct =>
        {
            var zone = await GetSiteZoneAsync(ct);
            var (production, consumption) = await ReadDestinationsAsync("Get power overview", "power", date, zone, ct);
            var levels = new Dictionary<DateTimeOffset, decimal?>();
            foreach (var (start, end) in Windows(AtSite(date.Date, zone), AtSite(date.Date.AddDays(1), zone)))
            {
                var response = await ReadTelemetryAsync("Get power overview", start, end, ct);
                var battery = response.Storage?.FirstOrDefault().Value;
                foreach (var value in battery?.StateOfEnergy?.Values ?? [])
                {
                    if (value.Timestamp >= start && value.Timestamp < end)
                    {
                        levels[value.Timestamp] = Percentage(value.Value, battery!.StateOfEnergy!.Unit);
                    }
                }
            }

            return new PowerOverviewResponse
            {
                Measurements = production.Keys.Union(consumption.Keys).Union(levels.Keys).Order().Select(timestamp => new PowerMeasurement
                {
                    MeasurementTime = TimeZoneInfo.ConvertTime(timestamp, zone).DateTime,
                    Production = production.GetValueOrDefault(timestamp)?.Total,
                    Consumption = consumption.GetValueOrDefault(timestamp)?.Total,
                    Export = production.GetValueOrDefault(timestamp)?.ToGrid,
                    Import = consumption.GetValueOrDefault(timestamp)?.FromGrid,
                    StorageLevel = levels.GetValueOrDefault(timestamp),
                    ProductionDistribution = Production(production.GetValueOrDefault(timestamp)),
                    ConsumptionDistribution = Consumption(consumption.GetValueOrDefault(timestamp))
                }).ToList()
            };
        }, cancellationToken);

    public Task<BatteryOverviewResponse> GetBatteryOverviewAsync(DateTime date, CancellationToken cancellationToken = default)
        => ExecuteAsync("Get battery overview", async ct =>
        {
            var zone = await GetSiteZoneAsync(ct);
            return await ReadStorageAsync("Get battery overview", AtSite(date.Date, zone), AtSite(date.Date.AddDays(1), zone), ct);
        }, cancellationToken);

    public Task<EnergyOverview> GetEnergyTodayAsync(CancellationToken cancellationToken = default)
        => GetEnergySummaryAsync("Get energy today", month: false, cancellationToken);

    public Task<EnergyOverview> GetEnergyThisMonthAsync(CancellationToken cancellationToken = default)
        => GetEnergySummaryAsync("Get energy this month", month: true, cancellationToken);

    public Task<StorageData> GetStorageDataAsync(StorageDataRange range, CancellationToken cancellationToken = default)
        => ExecuteAsync("Get storage data", async ct =>
        {
            var zone = await GetSiteZoneAsync(ct);
            var today = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).Date;
            var start = range switch
            {
                StorageDataRange.Today => today,
                StorageDataRange.ThreeDays => today.AddDays(-2),
                StorageDataRange.Week => today.AddDays(-5),
                StorageDataRange.Month => today.AddDays(-28),
                _ => throw new ArgumentOutOfRangeException(nameof(range), range, null)
            };
            var result = await ReadStorageAsync("Get storage data", AtSite(start, zone), timeProvider.GetUtcNow(), ct);
            return new StorageData(FirstBattery(result).Telemetries.Where(x => x.Level.HasValue)
                .Select(x => new StorageDataEntry(x.TimeStamp, x.Level!.Value)).ToList());
        }, cancellationToken);

    private Task<EnergyOverview> GetEnergySummaryAsync(string operation, bool month, CancellationToken cancellationToken)
        => ExecuteAsync(operation, async ct =>
        {
            var zone = await GetSiteZoneAsync(ct);
            var now = timeProvider.GetUtcNow();
            var date = TimeZoneInfo.ConvertTime(now, zone).Date;
            var start = month ? new DateTime(date.Year, date.Month, 1) : date;
            var result = await GetJsonAsync<SiteSummary>(operation, $"{Site}/overview?{Query(AtSite(start, zone), now)}", ct);
            var production = result.Production ?? throw ResponseError(operation, "Missing production totals.");
            var consumption = result.Consumption ?? throw ResponseError(operation, "Missing consumption totals.");
            return new EnergyOverview(
                Required(ConvertUnit(production.Total, production.Unit, true), "production total"),
                Required(ConvertUnit(consumption.Total, consumption.Unit, true), "consumption total"),
                Required(ConvertUnit(consumption.FromGrid, consumption.Unit, true), "grid import total"),
                Required(ConvertUnit(production.ToGrid, production.Unit, true), "grid export total"));
        }, cancellationToken);

    private Task<Measurements> GetProductionAsync(string operation, DateTimeOffset start, DateTimeOffset end, string resolution, CancellationToken ct)
        => GetJsonAsync<Measurements>(operation, $"{Site}/energy?{Query(start, end)}&resolution={resolution}", ct);

    private async Task<(Dictionary<DateTimeOffset, DestinationValue> Production, Dictionary<DateTimeOffset, DestinationValue> Consumption)>
        ReadDestinationsAsync(string operation, string metric, DateTime date, TimeZoneInfo zone, CancellationToken ct)
    {
        var production = new Dictionary<DateTimeOffset, DestinationValue>();
        var consumption = new Dictionary<DateTimeOffset, DestinationValue>();
        foreach (var (start, end) in Windows(AtSite(date.Date, zone), AtSite(date.Date.AddDays(1), zone)))
        {
            var response = await GetJsonAsync<DestinationResponse>(operation, $"{Site}/{metric}/per-destination?{Query(start, end)}&resolution=QUARTER_HOUR", ct);
            if (response.Production is null || response.Consumption is null)
            {
                throw ResponseError(operation, "Missing per-destination production or consumption series.");
            }

            Add(response.Production, production);
            Add(response.Consumption, consumption);

            void Add(DestinationSeries series, Dictionary<DateTimeOffset, DestinationValue> target)
            {
                foreach (var value in series.Values.Where(v => v.Timestamp >= start && v.Timestamp < end))
                {
                    decimal? Convert(decimal? number) => ConvertUnit(number, series.Unit, metric == "energy");
                    target[value.Timestamp] = value with
                    {
                        Total = Convert(value.Total),
                        ToSelfConsumption = Convert(value.ToSelfConsumption),
                        ToStorage = Convert(value.ToStorage),
                        ToGrid = Convert(value.ToGrid),
                        FromPv = Convert(value.FromPv),
                        FromStorage = Convert(value.FromStorage),
                        FromGrid = Convert(value.FromGrid)
                    };
                }
            }
        }

        return (production, consumption);
    }

    private Task<StorageTelemetryResponse> ReadTelemetryAsync(string operation, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
        => GetJsonAsync<StorageTelemetryResponse>(operation, $"{Site}/storage/telemetry?{Query(start, end)}&resolution=QUARTER_HOUR", ct);

    private async Task<BatteryOverviewResponse> ReadStorageAsync(string operation, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var zone = await GetSiteZoneAsync(ct);
        var devices = await cache.GetOrCreateAsync(("SolarEdge.Devices", HttpClient.BaseAddress, options.Value.SiteId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await GetJsonAsync<List<SolarDevice>>(operation, $"{Site}/devices?types=BATTERY", ct);
        }) ?? throw ResponseError(operation, "Missing battery inventory.");
        var batteries = new Dictionary<string, Dictionary<DateTimeOffset, BatteryTelemetry>>();
        foreach (var (from, to) in Windows(start, end))
        {
            var result = await ReadTelemetryAsync(operation, from, to, ct);
            if (result.Storage is null)
            {
                throw ResponseError(operation, "Missing storage telemetry.");
            }

            foreach (var (serial, metrics) in result.Storage)
            {
                if (!batteries.TryGetValue(serial, out var samples))
                {
                    samples = [];
                    batteries.Add(serial, samples);
                }

                var level = Values(metrics.StateOfEnergy, from, to);
                var remaining = Values(metrics.RemainingEnergy, from, to);
                var charge = Values(metrics.ChargePower, from, to);
                var discharge = Values(metrics.DischargePower, from, to);
                foreach (var timestamp in level.Keys.Union(remaining.Keys).Union(charge.Keys).Union(discharge.Keys))
                {
                    var soc = Percentage(level.GetValueOrDefault(timestamp), metrics.StateOfEnergy?.Unit);
                    var energy = ConvertUnit(remaining.GetValueOrDefault(timestamp), metrics.RemainingEnergy?.Unit, true);
                    var charging = ConvertUnit(charge.GetValueOrDefault(timestamp), metrics.ChargePower?.Unit, false);
                    var discharging = ConvertUnit(discharge.GetValueOrDefault(timestamp), metrics.DischargePower?.Unit, false);
                    samples[timestamp] = new BatteryTelemetry
                    {
                        TimeStamp = TimeZoneInfo.ConvertTime(timestamp, zone).DateTime,
                        Level = soc,
                        Power = charging.HasValue || discharging.HasValue ? ((charging ?? 0) - (discharging ?? 0)) * 1000 : null,
                        EnergyAvailable = soc > 0 && energy.HasValue ? energy / (soc / 100) : null
                    };
                }
            }
        }

        return new BatteryOverviewResponse
        {
            Storage = new StorageResponse
            {
                BatteryCount = batteries.Count,
                Batteries = batteries.Select(pair => new BatteryResponse
                {
                    SerialNumber = pair.Key,
                    Nameplate = devices.FirstOrDefault(d => d.SerialNumber == pair.Key)?.Nameplate ?? 0,
                    Telemetries = pair.Value.OrderBy(p => p.Key).Select(p => p.Value).ToList()
                }).ToList()
            }
        };
    }

    private async Task<TimeZoneInfo> GetSiteZoneAsync(CancellationToken ct)
        => await cache.GetOrCreateAsync(("SolarEdge.TimeZone", HttpClient.BaseAddress, options.Value.SiteId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            var metadata = await GetJsonAsync<SiteMetadata>("Get site timezone", Site, ct);
            var timezone = metadata.Location?.Timezone;
            if (string.IsNullOrWhiteSpace(timezone))
            {
                throw ResponseError("Get site timezone", "Site metadata did not contain location.timezone.");
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw ResponseError("Get site timezone", $"Unsupported site timezone '{timezone}'.");
            }
        }) ?? throw ResponseError("Get site timezone", "Missing site timezone.");

    private static Dictionary<DateTimeOffset, decimal?> Values(Measurements? values, DateTimeOffset from, DateTimeOffset to)
        => (values?.Values ?? []).Where(v => v.Timestamp >= from && v.Timestamp < to)
            .GroupBy(v => v.Timestamp).ToDictionary(g => g.Key, g => g.Last().Value);

    private static BatteryResponse FirstBattery(BatteryOverviewResponse result)
        => result.Storage?.Batteries.FirstOrDefault()
            ?? throw new InvalidDataException("No battery telemetry was returned for this site.");

    private static ProductionDistribution? Production(DestinationValue? value)
        => value is null ? null : new ProductionDistribution { ToHome = value.ToSelfConsumption, ToBattery = value.ToStorage, ToGrid = value.ToGrid };

    private static ConsumptionDistribution? Consumption(DestinationValue? value)
        => value is null ? null : new ConsumptionDistribution { FromSolar = value.FromPv, FromBattery = value.FromStorage, FromGrid = value.FromGrid };

    private static decimal Required(decimal? value, string field)
        => value ?? throw new InvalidDataException($"The response did not contain {field}.");

    private static int Direction(string? status, string positive, string negative)
        => status == positive ? 1 : status == negative ? -1 : status is "IDLE" or "GRID_OUTAGE" ? 0
            : throw new InvalidDataException($"Unknown power-flow status '{status}'.");

    private static decimal? Percentage(decimal? value, string? unit)
        => value is null ? null : unit is "PERCENTAGE" or "PERCENTAGE_100" ? value
            : throw new InvalidDataException($"Unexpected state-of-energy unit '{unit}'.");

    private static decimal ConvertUnit(decimal value, string unit, bool energy)
        => ConvertUnit((decimal?)value, unit, energy)!.Value;

    private static decimal? ConvertUnit(decimal? value, string? unit, bool energy)
    {
        if (value is null)
        {
            return null;
        }

        var factor = (energy, unit?.ToUpperInvariant()) switch
        {
            (true, "WH") => 1m,
            (true, "KWH") => 1000m,
            (true, "MWH") => 1000000m,
            (true, "GWH") => 1000000000m,
            (false, "W") => 0.001m,
            (false, "KW") => 1m,
            (false, "MW") => 1000m,
            (false, "GW") => 1000000m,
            _ => throw new InvalidDataException($"Unsupported SolarEdge unit '{unit}'.")
        };
        return value * factor;
    }

    private static DateTimeOffset AtSite(DateTime date, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static string Query(DateTimeOffset start, DateTimeOffset end)
        => $"from={Uri.EscapeDataString(start.ToString("O", CultureInfo.InvariantCulture))}&to={Uri.EscapeDataString(end.ToString("O", CultureInfo.InvariantCulture))}";

    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> Windows(DateTimeOffset start, DateTimeOffset end)
    {
        while (start < end)
        {
            var next = start.AddHours(12) < end ? start.AddHours(12) : end;
            yield return (start, next);
            start = next;
        }
    }
}
