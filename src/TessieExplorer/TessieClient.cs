using System.Net.Http.Headers;
using System.Text.Json;

namespace TessieExplorer;

public enum VehicleSection
{
    State,
    BatteryHealth,
    Location,
    Charges,
    Drives
}

public sealed class TessieClient
{
    private readonly HttpClient http;
    private readonly string token;
    private readonly string vin;

    public TessieClient(HttpClient http, string token, string vin)
    {
        this.http = http;
        this.token = token;
        this.vin = vin;
    }

    public async Task<JsonElement> GetAsync(VehicleSection section, CancellationToken cancellationToken = default)
    {
        var path = section switch
        {
            VehicleSection.State => $"{Uri.EscapeDataString(vin)}/state",
            VehicleSection.BatteryHealth => "battery_health",
            VehicleSection.Location => $"{Uri.EscapeDataString(vin)}/location",
            VehicleSection.Charges => $"{Uri.EscapeDataString(vin)}/charges?distance_format=km",
            VehicleSection.Drives => $"{Uri.EscapeDataString(vin)}/drives?distance_format=km&temperature_format=c",
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Unexpected response format");
        }

        if (section is VehicleSection.BatteryHealth or VehicleSection.Charges or VehicleSection.Drives &&
            (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array))
        {
            throw new InvalidDataException("Response has no results array");
        }

        return root.Clone();
    }
}
