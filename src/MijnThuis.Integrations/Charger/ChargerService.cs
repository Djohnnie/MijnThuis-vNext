using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MijnThuis.Integrations.Common;

namespace MijnThuis.Integrations.Charger;

public sealed record ChargerOverview
{
    public required string ChargerId { get; init; }
    public required string Description { get; init; }
    public required int NumberOfChargers { get; init; }
    public required int NumberOfChargersAvailable { get; init; }
}

public interface IChargerService
{
    /// <summary>
    /// Gets the availability of a public charge point, or <c>null</c> when the charge point is unknown.
    /// </summary>
    Task<ChargerOverview?> GetChargerOverviewAsync(string chargerId, CancellationToken cancellationToken = default);
}

internal sealed class ChargerService(HttpClient httpClient, ILogger<ChargerService> logger)
    : HttpIntegrationServiceBase(httpClient, logger), IChargerService
{
    private const string AvailableStatus = "Available";

    protected override string IntegrationName => IntegrationNames.Charger;

    public Task<ChargerOverview?> GetChargerOverviewAsync(string chargerId, CancellationToken cancellationToken = default)
    {
        const string operation = "Get charger overview";

        return ExecuteAsync<ChargerOverview?>(operation, async ct =>
        {
            var response = await GetJsonOrDefaultAsync<GetChargePointResponse>(operation, $"api/chargepoint?id={Uri.EscapeDataString(chargerId)}", ct);
            var chargePoint = response?.ChargePoint;

            if (chargePoint is null)
            {
                return null;
            }

            return new ChargerOverview
            {
                ChargerId = $"{chargePoint.Id}",
                Description = chargePoint.Name ?? chargerId,
                NumberOfChargers = chargePoint.Connectors.Count,
                NumberOfChargersAvailable = chargePoint.Connectors.Count(x => x.Status == AvailableStatus)
            };
        }, cancellationToken);
    }

    private sealed class GetChargePointResponse
    {
        [JsonPropertyName("chargepoint")]
        public ChargePoint? ChargePoint { get; set; }
    }

    private sealed class ChargePoint
    {
        [JsonPropertyName("uid")]
        public int Id { get; set; }

        [JsonPropertyName("externalId")]
        public string? Name { get; set; }

        [JsonPropertyName("evses")]
        public List<Connector> Connectors { get; set; } = [];
    }

    private sealed class Connector
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
