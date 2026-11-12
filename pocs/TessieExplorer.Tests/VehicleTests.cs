using System.Net;
using System.Text.Json;
using Spectre.Console;

namespace TessieExplorer.Tests;

public class VehicleTests
{
    private const string Vin = "5YJYGDEE0LF000001";

    [Fact]
    public async Task RequestsUseBearerHeaderAndMetricHistoryUnits()
    {
        var handler = new StubHandler("""{"results":[]}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.tessie.com/") };
        var client = new TessieClient(http, "test-token", Vin);

        await client.GetAsync(VehicleSection.Drives, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal($"/{Vin}/drives?distance_format=km&temperature_format=c", handler.Path);
        Assert.Equal("Bearer", handler.Scheme);
        Assert.Equal("test-token", handler.Token);
        await client.GetAsync(VehicleSection.Charges, TestContext.Current.CancellationToken);
        Assert.Equal($"/{Vin}/charges?distance_format=km", handler.Path);
        await client.GetAsync(VehicleSection.BatteryHealth, TestContext.Current.CancellationToken);
        Assert.Equal("/battery_health", handler.Path);
        await client.GetAsync(VehicleSection.Location, TestContext.Current.CancellationToken);
        Assert.Equal($"/{Vin}/location", handler.Path);
        await client.GetAsync(VehicleSection.State, TestContext.Current.CancellationToken);
        Assert.Equal($"/{Vin}/state", handler.Path);
    }

    [Fact]
    public async Task HttpAndMalformedResultsFail()
    {
        using var http = new HttpClient(new StubHandler("not shown", HttpStatusCode.Unauthorized))
            { BaseAddress = new Uri("https://api.tessie.com/") };
        var client = new TessieClient(http, "test-token", Vin);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(VehicleSection.State, TestContext.Current.CancellationToken));
        Assert.Contains("401", error.Message);
        Assert.DoesNotContain("not shown", error.Message);
        Assert.DoesNotContain("test-token", error.Message);

        using var malformedHttp = new HttpClient(new StubHandler("{}")) { BaseAddress = new Uri("https://api.tessie.com/") };
        var malformed = new TessieClient(malformedHttp, "test-token", Vin);
        await Assert.ThrowsAsync<InvalidDataException>(() => malformed.GetAsync(VehicleSection.Charges, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StateDisplaysConvertedRangeAndMissingValues()
    {
        var output = Display(VehicleSection.State, """
            {"vehicle_state":{"locked":false},"charge_state":{"charging_state":"Charging","usable_battery_level":72,
             "est_battery_range":100,"charge_miles_added_rated":10,"charge_energy_added":2.5,
             "charger_actual_current":16,"charge_current_request_max":32,"charge_limit_soc":80,"charge_port_door_open":true},
             "climate_state":{"inside_temp":21,"outside_temp":-2,"is_preconditioning":true,"cabin_overheat_protection":"On"}}
            """);
        Assert.Contains("160.93 km", output);
        Assert.Contains("16.09 km", output);
        Assert.Contains("21 °C", output);
        Assert.Contains("-2 °C", output);
        Assert.Contains("Charging", output);
        Assert.Contains("Unavailable", output);
        Assert.Contains("2.5 kWh", output);
    }

    [Fact]
    public void BatteryHealthSelectsMatchingVinAndLocationEscapesMarkup()
    {
        var output = Display(VehicleSection.BatteryHealth, """
            {"results":[{"vin":"OTHER","health_percent":20},{"vin":"5yjygdee0lf000001","health_percent":91.5}]}
            """);
        Assert.Contains("91.5 %", output);
        Assert.DoesNotContain("20 %", output);
        Assert.Contains("unavailable", Display(VehicleSection.BatteryHealth, """{"results":[]}"""));
        var location = Display(VehicleSection.Location, """{"address":"[Home]","saved_location":null}""");
        Assert.Contains("[Home]", location);
        Assert.Contains("Unavailable", location);
    }

    [Fact]
    public void HistoriesShowEachRecordAndLocalTimestamp()
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(1700000000).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
        var charges = Display(VehicleSection.Charges, """
            {"results":[{"id":123,"started_at":1700000000,"ended_at":1700003600,"location":"Home",
            "saved_location":"Garage","is_supercharger":false,"is_fast_charger":true,"odometer":2000,
            "energy_added":30,"energy_used":32,"miles_added":150,"starting_battery":20,"ending_battery":80,
            "since_last_charge":120},{"id":456}]}
            """);
        Assert.Contains("123", charges);
        Assert.Contains("456", charges);
        Assert.Contains(local, charges);
        Assert.Contains("150 km", charges);
        Assert.Contains("32 kWh", charges);
        Assert.Contains("Unavailable", charges);
        var drives = Display(VehicleSection.Drives, """
            {"results":[{"id":987,"started_at":1700000000,"ended_at":1700003600,
            "starting_location":"A","ending_location":"B","starting_odometer":2000,"ending_odometer":2050,
            "starting_battery":80,"ending_battery":60,"energy_used":10,"rated_range_used":60,
            "average_speed":50,"max_speed":100,"odometer_distance":50,
            "average_inside_temperature":20,"average_outside_temperature":5}]}
            """);
        Assert.Contains("987", drives);
        Assert.Contains(local, drives);
        Assert.Contains("100 km/h", drives);
        Assert.Contains("20 °C", drives);
        Assert.Contains("No history available", Display(VehicleSection.Drives, """{"results":[]}"""));
    }

    private static string Display(VehicleSection section, string json)
    {
        using var document = JsonDocument.Parse(json);
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer)
        });
        new VehiclePresenter(console).Display(section, document.RootElement, Vin);
        return writer.ToString();
    }

    private sealed class StubHandler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Scheme { get; private set; }
        public string? Token { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.PathAndQuery;
            Scheme = request.Headers.Authorization?.Scheme;
            Token = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) });
        }
    }
}
