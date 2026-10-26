using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MijnThuis.Integrations.Common;

namespace MijnThuis.Integrations.Power;

public sealed record PowerOverview
{
    public required byte ActiveTariff { get; init; }
    public required decimal TotalImport { get; init; }
    public required decimal Tariff1Import { get; init; }
    public required decimal Tariff2Import { get; init; }
    public required decimal TotalExport { get; init; }
    public required decimal Tariff1Export { get; init; }
    public required decimal Tariff2Export { get; init; }
    public required decimal TotalGas { get; init; }
    public required int CurrentPower { get; init; }
    public required int PowerPeak { get; init; }
}

public interface IPowerService
{
    Task<PowerOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// HomeWizard P1 meter through its local API (https://api-documentation.homewizard.com).
/// </summary>
internal sealed class PowerService(HttpClient httpClient, ILogger<PowerService> logger)
    : HttpIntegrationServiceBase(httpClient, logger), IPowerService
{
    protected override string IntegrationName => IntegrationNames.Power;

    public Task<PowerOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        const string operation = "Get overview";

        return ExecuteAsync(operation, async ct =>
        {
            var result = await GetJsonAsync<DataResponse>(operation, "api/v1/data", ct);

            return new PowerOverview
            {
                ActiveTariff = result.ActiveTariff,
                TotalImport = result.TotalImport ?? 0M,
                Tariff1Import = result.Tariff1Import ?? 0M,
                Tariff2Import = result.Tariff2Import ?? 0M,
                TotalExport = result.TotalExport ?? 0M,
                Tariff1Export = result.Tariff1Export ?? 0M,
                Tariff2Export = result.Tariff2Export ?? 0M,
                TotalGas = result.TotalGas ?? 0M,
                CurrentPower = (int)(result.CurrentPower ?? 0M),
                PowerPeak = (int)(result.PowerPeak ?? 0M)
            };
        }, cancellationToken);
    }

    private sealed class DataResponse
    {
        [JsonPropertyName("active_tariff")]
        public byte ActiveTariff { get; set; }

        [JsonPropertyName("total_power_import_kwh")]
        public decimal? TotalImport { get; set; }

        [JsonPropertyName("total_power_import_t1_kwh")]
        public decimal? Tariff1Import { get; set; }

        [JsonPropertyName("total_power_import_t2_kwh")]
        public decimal? Tariff2Import { get; set; }

        [JsonPropertyName("total_power_export_kwh")]
        public decimal? TotalExport { get; set; }

        [JsonPropertyName("total_power_export_t1_kwh")]
        public decimal? Tariff1Export { get; set; }

        [JsonPropertyName("total_power_export_t2_kwh")]
        public decimal? Tariff2Export { get; set; }

        [JsonPropertyName("total_gas_m3")]
        public decimal? TotalGas { get; set; }

        [JsonPropertyName("active_power_w")]
        public decimal? CurrentPower { get; set; }

        [JsonPropertyName("montly_power_peak_w")]
        public decimal? PowerPeak { get; set; }
    }
}
