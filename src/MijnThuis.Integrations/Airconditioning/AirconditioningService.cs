using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Airconditioning.Gree;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Resilience;
using Polly;
using Polly.Registry;

namespace MijnThuis.Integrations.Airconditioning;

public interface IAirconditioningService
{
    Task<AirconditioningOverview> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task TurnOnAsync(CancellationToken cancellationToken = default);

    Task TurnOffAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the target temperature. The value is clamped to the supported range (16-30 °C).
    /// </summary>
    Task SetTargetTemperatureAsync(int temperature, CancellationToken cancellationToken = default);

    Task IncreaseTargetTemperatureAsync(CancellationToken cancellationToken = default);

    Task DecreaseTargetTemperatureAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Controls a Gree Wi-Fi air conditioning unit over its local UDP protocol.
/// </summary>
internal sealed class AirconditioningService(
    GreeClient client,
    GreeDeviceRegistry deviceRegistry,
    IOptions<AirconditioningOptions> options,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<AirconditioningService> logger) : IntegrationServiceBase(logger), IAirconditioningService
{
    private const string ColPower = "Pow";
    private const string ColMode = "Mod";
    private const string ColSetTemp = "SetTem";
    private const string ColTempUnit = "TemUn";
    private const string ColRoomTemp = "TemSen";
    private const string ColFanSpeed = "WdSpd";

    // The room temperature sensor value has a +40 offset to avoid negative numbers on the wire.
    private const int RoomTempOffset = 40;
    private const int MinTargetTemperature = 16;
    private const int MaxTargetTemperature = 30;

    private readonly ResiliencePipeline _pipeline = pipelineProvider.GetPipeline(IntegrationResiliencePipelines.LocalDevice);

    protected override string IntegrationName => IntegrationNames.Airconditioning;

    public Task<AirconditioningOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("Get overview", GetOverviewCoreAsync, cancellationToken);

    public Task TurnOnAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("Turn on", ct => SetParametersAsync(new Dictionary<string, int> { [ColPower] = 1 }, ct), cancellationToken);

    public Task TurnOffAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("Turn off", ct => SetParametersAsync(new Dictionary<string, int> { [ColPower] = 0 }, ct), cancellationToken);

    public Task SetTargetTemperatureAsync(int temperature, CancellationToken cancellationToken = default)
        => ExecuteAsync("Set target temperature", ct => SetTargetTemperatureCoreAsync(temperature, ct), cancellationToken);

    public Task IncreaseTargetTemperatureAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("Increase target temperature", ct => AdjustTargetTemperatureAsync(+1, ct), cancellationToken);

    public Task DecreaseTargetTemperatureAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("Decrease target temperature", ct => AdjustTargetTemperatureAsync(-1, ct), cancellationToken);

    private async Task<AirconditioningOverview> GetOverviewCoreAsync(CancellationToken cancellationToken)
    {
        var status = await ExecuteOnDeviceAsync((device, ip, ct) =>
            client.GetStatusAsync(device, ip, [ColPower, ColMode, ColSetTemp, ColRoomTemp, ColFanSpeed], ct), cancellationToken);

        var isOn = status.GetValueOrDefault(ColPower) == 1;

        return new AirconditioningOverview
        {
            IsOn = isOn,
            RoomTemperature = status.GetValueOrDefault(ColRoomTemp) - RoomTempOffset,
            TargetTemperature = status.GetValueOrDefault(ColSetTemp),
            Mode = isOn ? MapMode(status.GetValueOrDefault(ColMode)) : "Uit",
            FanSpeed = MapFanSpeed(status.GetValueOrDefault(ColFanSpeed))
        };
    }

    private async Task AdjustTargetTemperatureAsync(int delta, CancellationToken cancellationToken)
    {
        var overview = await GetOverviewCoreAsync(cancellationToken);
        await SetTargetTemperatureCoreAsync((int)overview.TargetTemperature + delta, cancellationToken);
    }

    private Task SetTargetTemperatureCoreAsync(int temperature, CancellationToken cancellationToken)
    {
        var clamped = Math.Clamp(temperature, MinTargetTemperature, MaxTargetTemperature);

        return SetParametersAsync(new Dictionary<string, int> { [ColTempUnit] = 0, [ColSetTemp] = clamped }, cancellationToken);
    }

    private Task SetParametersAsync(IReadOnlyDictionary<string, int> parameters, CancellationToken cancellationToken)
        => ExecuteOnDeviceAsync(async (device, ip, ct) =>
        {
            await client.SetParametersAsync(device, ip, parameters, ct);
            return true;
        }, cancellationToken);

    /// <summary>
    /// Runs an operation against the bound device (binding first when needed). When the operation fails with a
    /// cached binding, the binding is dropped and the operation is retried once with a fresh binding.
    /// Transient network failures are retried by the local-device resilience pipeline.
    /// </summary>
    private Task<T> ExecuteOnDeviceAsync<T>(Func<BoundGreeDevice, IPAddress, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var ipAddress = IPAddress.Parse(settings.IpAddress);

        return _pipeline.ExecuteAsync(async ct =>
        {
            if (deviceRegistry.TryGet(settings.MacAddress, out var device))
            {
                try
                {
                    return await operation(device, ipAddress, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.LogInformation(ex, "Operation on cached Gree binding failed, re-binding to {MacAddress}", settings.MacAddress);
                    deviceRegistry.Remove(settings.MacAddress);
                }
            }

            device = await client.BindAsync(settings.MacAddress, ipAddress, ct)
                ?? throw new TimeoutException($"Could not bind to the air conditioning unit at {ipAddress}. Check its IP/MAC address and make sure it's reachable.");

            deviceRegistry.Set(device);

            return await operation(device, ipAddress, ct);
        }, cancellationToken).AsTask();
    }

    private static string MapMode(int mode) => mode switch
    {
        1 => "Koelen",
        2 => "Drogen",
        3 => "Ventileren",
        4 => "Verwarmen",
        _ => "Automatisch"
    };

    private static string MapFanSpeed(int fanSpeed) => fanSpeed switch
    {
        1 => "Laag",
        2 => "Medium-laag",
        3 => "Medium",
        4 => "Medium-hoog",
        5 => "Hoog",
        _ => "Automatisch"
    };
}
