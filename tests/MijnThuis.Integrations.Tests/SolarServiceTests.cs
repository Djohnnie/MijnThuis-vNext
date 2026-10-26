using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common.Exceptions;
using MijnThuis.Integrations.Solar;

namespace MijnThuis.Integrations.Tests;

public class SolarServiceTests
{
    [Theory]
    [InlineData("W", 1000, 1)]
    [InlineData("kW", 1, 1)]
    public async Task Overview_MapsV2PowerflowAndDirections(string unit, decimal value, decimal expected)
    {
        using var fixture = new Fixture(request =>
        {
            Assert.Equal("/v2/sites/42/power-flow", request.RequestUri!.AbsolutePath);
            return Json(new
            {
                unit,
                updatedAt = "2026-10-25T12:00:00+01:00",
                refreshRate = 300,
                pv = new { active = true, power = value },
                load = new { active = true, power = value * 2 },
                storage = new { active = true, power = value * 3, status = "DISCHARGE", chargeLevel = 75 },
                grid = new { active = true, power = value * 4, status = "EXPORT" }
            });
        });

        var result = await fixture.Service.GetOverviewAsync();

        Assert.Equal(expected, result.CurrentSolarPower);
        Assert.Equal(2 * expected, result.CurrentConsumptionPower);
        Assert.Equal(-3 * expected, result.CurrentBatteryPower);
        Assert.Equal(-4 * expected, result.CurrentGridPower);
        Assert.Equal(75, result.BatteryLevel);
        Assert.Equal(300, result.RefreshRate);
        Assert.NotNull(result.UpdatedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Summary_UsesSiteLocalTodayOrMonthAndConvertsUnits(bool month)
    {
        using var fixture = new Fixture(request =>
        {
            Assert.Equal("/v2/sites/42/overview", request.RequestUri!.AbsolutePath);
            var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
            var from = DateTimeOffset.Parse(query["from"]!, CultureInfo.InvariantCulture);
            Assert.Equal(month ? 1 : 25, from.Day);
            Assert.Equal(TimeSpan.FromHours(2), from.Offset); // midnight precedes the autumn transition
            Assert.Equal(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero),
                DateTimeOffset.Parse(query["to"]!, CultureInfo.InvariantCulture));
            return Json(new
            {
                production = new { unit = "KWH", total = 10, toGrid = 2 },
                consumption = new { unit = "WH", total = 12000, fromGrid = 4000 }
            });
        });

        var result = month ? await fixture.Service.GetEnergyThisMonthAsync() : await fixture.Service.GetEnergyTodayAsync();

        Assert.Equal(new EnergyOverview(10000, 12000, 4000, 2000), result);
    }

    [Fact]
    public async Task Overview_InactiveAndIdleComponentsDoNotRequirePowerOrDirection()
    {
        using var fixture = new Fixture(_ => Json(new
        {
            unit = "kW",
            updatedAt = "2026-10-25T12:00:00+01:00",
            refreshRate = 300,
            pv = new { active = false },
            load = new { active = true, power = 1m },
            storage = new { active = false, chargeLevel = 75 },
            grid = new { active = true, status = "IDLE" }
        }));

        var result = await fixture.Service.GetOverviewAsync();

        Assert.Equal(0, result.CurrentSolarPower);
        Assert.Equal(0, result.CurrentBatteryPower);
        Assert.Equal(0, result.CurrentGridPower);
        Assert.Equal(75, result.BatteryLevel);
    }

    [Fact]
    public async Task EnergyProduced_UsesDaySeriesAndSiteLocalDates()
    {
        using var fixture = new Fixture(request =>
        {
            Assert.Equal("/v2/sites/42/energy", request.RequestUri!.AbsolutePath);
            Assert.Contains("resolution=DAY", request.RequestUri.Query);
            return Json(new
            {
                unit = "KWH",
                values = new[]
                {
                    new { timestamp = "2026-10-24T00:00:00+02:00", value = 3 },
                    new { timestamp = "2026-10-25T00:00:00+02:00", value = 4 }
                }
            });
        });

        var result = await fixture.Service.GetEnergyAsync();

        Assert.Equal(new EnergyProduced(4000, 7000), result);
    }

    [Theory]
    [InlineData("2026-03-29", 2, 23)]
    [InlineData("2026-06-15", 2, 24)]
    [InlineData("2026-10-25", 3, 25)]
    public async Task EnergyChart_SplitsQuarterHourWindowsIncludingDst(string date, int calls, int hours)
    {
        var windows = new List<(DateTimeOffset From, DateTimeOffset To)>();
        using var fixture = new Fixture(request =>
        {
            Assert.Equal("/v2/sites/42/energy/per-destination", request.RequestUri!.AbsolutePath);
            var window = ReadWindow(request);
            windows.Add(window);
            // Include the end boundary: it must not appear twice in the merged chart.
            return Destinations(window.From, window.To);
        });

        var result = await fixture.Service.GetEnergyOverviewAsync(DateTime.Parse(date, CultureInfo.InvariantCulture));

        Assert.Equal(calls, windows.Count);
        Assert.Equal(hours, (windows[^1].To - windows[0].From).TotalHours);
        Assert.All(windows, w => Assert.InRange((w.To - w.From).TotalHours, 0, 12));
        for (var i = 1; i < windows.Count; i++)
        {
            Assert.Equal(windows[i - 1].To, windows[i].From);
        }
        Assert.Equal(calls, result.Chart!.Measurements.Count);
        Assert.Equal(calls * 1000, result.Summary!.Production);
        Assert.All(result.Chart.Measurements, sample =>
        {
            Assert.Equal(1000, sample.Production);
            Assert.Equal(500, sample.Consumption);
            Assert.Equal(200, sample.Export);
            Assert.Equal(100, sample.Import);
            Assert.Equal(300, sample.ProductionDistribution!.ToHome);
            Assert.Equal(400, sample.ConsumptionDistribution!.FromBattery);
        });
    }

    [Fact]
    public async Task PowerChart_MergesPerDestinationAndStorageState()
    {
        using var fixture = new Fixture(request =>
        {
            var (from, to) = ReadWindow(request);
            return request.RequestUri!.AbsolutePath.EndsWith("/power/per-destination")
                ? Destinations(from, to, "KW")
                : Telemetry(from);
        });

        var result = await fixture.Service.GetPowerOverviewAsync(new DateTime(2026, 6, 15));

        Assert.Equal(2, result.Measurements.Count);
        Assert.All(result.Measurements, value =>
        {
            Assert.Equal(1, value.Production);
            Assert.Equal(50, value.StorageLevel);
            Assert.Equal(0.2m, value.Export);
        });
    }

    [Fact]
    public async Task BatteryOverview_ReconstructsCapacityAndNetPowerFromTelemetry()
    {
        using var fixture = new Fixture(request =>
        {
            var (from, _) = ReadWindow(request);
            return Telemetry(from);
        });

        var result = await fixture.Service.GetBatteryOverviewAsync(new DateTime(2026, 6, 15));

        var battery = Assert.Single(result.Storage!.Batteries);
        Assert.Equal("BAT-1", battery.SerialNumber);
        Assert.Equal(10000, battery.Nameplate);
        Assert.Equal(2, battery.Telemetries.Count);
        Assert.All(battery.Telemetries, sample =>
        {
            Assert.Equal(50, sample.Level);
            Assert.Equal(8000, sample.EnergyAvailable);
            Assert.Equal(1500, sample.Power);
        });
    }

    [Fact]
    public async Task BatteryLevel_UsesUsableCapacityForHealth()
    {
        using var fixture = new Fixture(request => Telemetry(ReadWindow(request).From));

        var result = await fixture.Service.GetBatteryLevelAsync();

        Assert.Equal(50, result.Level);
        Assert.Equal(80, result.Health);
        Assert.Equal(8000, result.MaxEnergy);
    }

    [Fact]
    public async Task BatteryLevel_ExplainsWhenEmptyBatteryPreventsCapacityCalculation()
    {
        using var fixture = new Fixture(request => Telemetry(ReadWindow(request).From, 0));

        var error = await Assert.ThrowsAsync<IntegrationResponseException>(() => fixture.Service.GetBatteryLevelAsync());

        Assert.Contains("Battery health is unavailable", error.Message);
    }

    [Theory]
    [InlineData(StorageDataRange.Today)]
    [InlineData(StorageDataRange.ThreeDays)]
    [InlineData(StorageDataRange.Week)]
    [InlineData(StorageDataRange.Month)]
    public async Task StorageHistory_SplitsEveryRequestToAtMostTwelveHours(StorageDataRange range)
    {
        var calls = 0;
        using var fixture = new Fixture(request =>
        {
            var (from, _) = ReadWindow(request);
            calls++;
            return Telemetry(from);
        });

        var result = await fixture.Service.GetStorageDataAsync(range);

        Assert.Equal(calls, result.Entries.Count);
        Assert.All(result.Entries, entry => Assert.Equal(50, entry.ChargeState));
        Assert.True(result.Entries.SequenceEqual(result.Entries.OrderBy(x => x.Timestamp)));
    }

    [Fact]
    public async Task InvalidResponse_IsNotReplacedWithZeroMeasurements()
    {
        using var fixture = new Fixture(_ => Json(new { }));

        await Assert.ThrowsAsync<IntegrationResponseException>(() => fixture.Service.GetOverviewAsync());
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var fixture = new Fixture(_ => throw new InvalidOperationException("No requests expected."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.GetOverviewAsync(cancellation.Token));
    }

    private static HttpResponseMessage Destinations(DateTimeOffset from, DateTimeOffset to, string unit = "KWH")
        => Json(new
        {
            production = new
            {
                unit,
                values = new[] { from, to }.Select(timestamp => new { timestamp, total = 1, toSelfConsumption = 0.3m, toStorage = 0.5m, toGrid = 0.2m })
            },
            consumption = new
            {
                unit,
                values = new[] { from, to }.Select(timestamp => new { timestamp, total = 0.5m, fromPv = 0m, fromStorage = 0.4m, fromGrid = 0.1m })
            }
        });

    private static HttpResponseMessage Telemetry(DateTimeOffset timestamp, decimal stateOfEnergy = 50)
        => Json(new
        {
            storage = new Dictionary<string, object>
            {
                ["BAT-1"] = new
                {
                    stateOfEnergy = Series("PERCENTAGE", timestamp, stateOfEnergy),
                    remainingEnergy = Series("KWH", timestamp, 4),
                    chargePower = Series("W", timestamp, 2000),
                    dischargePower = Series("KW", timestamp, 0.5m)
                }
            }
        });

    private static object Series(string unit, DateTimeOffset timestamp, decimal value)
        => new { unit, values = new[] { new { timestamp, value } } };

    private static (DateTimeOffset From, DateTimeOffset To) ReadWindow(HttpRequestMessage request)
    {
        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        Assert.Equal("QUARTER_HOUR", query["resolution"]);
        var from = DateTimeOffset.Parse(query["from"]!, CultureInfo.InvariantCulture);
        var to = DateTimeOffset.Parse(query["to"]!, CultureInfo.InvariantCulture);
        Assert.InRange((to - from).TotalHours, 0, 12);
        Assert.Null(query["api_key"]);
        return (from, to);
    }

    private static HttpResponseMessage Json(object value)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _client;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public SolarService Service { get; }

        public Fixture(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _client = new HttpClient(new Handler(request =>
            {
                Assert.DoesNotContain("/site/", request.RequestUri!.AbsolutePath);
                return request.RequestUri.AbsolutePath switch
                {
                    "/v2/sites/42" => Json(new { location = new { timezone = "Europe/Brussels" } }),
                    "/v2/sites/42/devices" => Json(new[] { new { serialNumber = "BAT-1", nameplate = 10000 } }),
                    _ => respond(request)
                };
            }))
            { BaseAddress = new Uri("https://solar.example/v2/") };
            Service = new SolarService(_client, Options.Create(new SolarOptions { SiteId = "42", AuthToken = "test-key" }),
                new FixedTimeProvider(), _cache, NullLogger<SolarService>.Instance);
        }

        public void Dispose()
        {
            _client.Dispose();
            _cache.Dispose();
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
