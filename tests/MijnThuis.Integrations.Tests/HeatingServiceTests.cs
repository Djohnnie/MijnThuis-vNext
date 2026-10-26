using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MijnThuis.Integrations.Common.Http;
using MijnThuis.Integrations.Heating;

namespace MijnThuis.Integrations.Tests;

public class HeatingServiceTests
{
    private const string Dashboard = """
        {
          "appliances": [{
            "outdoorTemperatureInformation": { "cloudOutdoorTemperature": 12.5 },
            "climateZones": [{
              "climateZoneId": "zone-1",
              "roomTemperature": 20.5,
              "zoneMode": "manual",
              "setpoint": 21,
              "nextSetpoint": 19,
              "nextSwitchTime": null
            }]
          }]
        }
        """;

    [Theory]
    [InlineData("https://heating.example/Mobile/api")]
    [InlineData("https://heating.example/Mobile/api/")]
    public async Task GetOverview_RequestsDashboardUnderConfiguredApiPath(string baseAddress)
    {
        using var handler = new HeatingHandler();
        using var client = new HttpClient(handler) { BaseAddress = baseAddress.ToBaseAddress() };
        var service = new HeatingService(client, NullLogger<HeatingService>.Instance);

        var overview = await service.GetOverviewAsync();

        Assert.Equal(["https://heating.example/Mobile/api/homes/dashboard"], handler.RequestUris);
        Assert.Equal("manual", overview.Mode);
        Assert.Equal(20.5m, overview.RoomTemperature);
        Assert.Equal(21m, overview.Setpoint);
        Assert.Equal(12.5m, overview.OutdoorTemperature);
    }

    [Theory]
    [InlineData("manual", "roomTemperatureSetPoint", 21)]
    [InlineData("temporary-override", "roomTemperatureSetPoint", 22)]
    [InlineData("schedule", "heatingProgramId", 2)]
    [InlineData("anti-frost", null, 0)]
    public async Task Commands_PostToZoneUnderConfiguredApiPath(string mode, string? property, int value)
    {
        using var handler = new HeatingHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = "https://heating.example/Mobile/api".ToBaseAddress()
        };
        var service = new HeatingService(client, NullLogger<HeatingService>.Instance);

        await (mode switch
        {
            "manual" => service.SetManualHeatingAsync(value),
            "temporary-override" => service.SetTemporaryOverrideHeatingAsync(value),
            "schedule" => service.SetScheduledHeatingAsync(value),
            "anti-frost" => service.SetAntiFrostHeatingAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        });

        Assert.Equal(new[]
        {
            "https://heating.example/Mobile/api/homes/dashboard",
            $"https://heating.example/Mobile/api/climate-zones/zone-1/modes/{mode}"
        }, handler.RequestUris);
        using var body = JsonDocument.Parse(Assert.IsType<string>(handler.PostBody));
        if (property is null)
        {
            Assert.Empty(body.RootElement.EnumerateObject());
        }
        else
        {
            Assert.Equal(value, body.RootElement.GetProperty(property).GetDecimal());
        }
    }

    private sealed class HeatingHandler : HttpMessageHandler
    {
        public List<string> RequestUris { get; } = [];
        public string? PostBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = Assert.IsType<Uri>(request.RequestUri);
            RequestUris.Add(uri.AbsoluteUri);
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/Mobile/api/homes/dashboard", uri.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Dashboard, Encoding.UTF8, "application/json")
                };
            }

            Assert.Equal(HttpMethod.Post, request.Method);
            PostBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
