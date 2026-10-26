using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Configuration;

namespace MijnThuis.Integrations.SmartLock;

public sealed class SmartLockOptions
{
    [Required]
    [Url]
    [ConfigurationKeyName("SMARTLOCK_API_BASE_ADDRESS")]
    public string BaseAddress { get; set; } = "https://api.nuki.io/";

    [Required]
    [Secret]
    [ConfigurationKeyName("SMARTLOCK_API_AUTH_TOKEN")]
    public string AuthToken { get; set; } = string.Empty;

    [Required]
    [ConfigurationKeyName("SMARTLOCK_ID")]
    public string SmartLockId { get; set; } = string.Empty;
}

public enum SmartLockState
{
    Uncalibrated = 0,
    Locked = 1,
    Unlocking = 2,
    Unlocked = 3,
    Locking = 4,
    Unlatched = 5,
    UnlockedLockNGo = 6,
    Unlatching = 7,
    MotorBlocked = 254,
    Undefined = 255
}

public enum SmartLockDoorState
{
    Unavailable = 0,
    Deactivated = 1,
    DoorClosed = 2,
    DoorOpened = 3,
    DoorStateUnknown = 4,
    Calibrating = 5
}

public enum SmartLockAction
{
    Unlock = 1,
    Lock = 2,
    Unlatch = 3,
    LockNGo = 4,
    LockNGoWithUnlatch = 5,
    DoorOpened = 240,
    DoorClosed = 241
}

public sealed record SmartLockOverview(SmartLockState State, SmartLockDoorState DoorState, int BatteryCharge);

public sealed record SmartLockLog(DateTime Timestamp, SmartLockAction Action);

public interface ISmartLockService
{
    Task<SmartLockOverview> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SmartLockLog>> GetActivityLogAsync(int limit = 10, CancellationToken cancellationToken = default);

    Task UnlockAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Nuki smart lock through the Nuki Web API (https://api.nuki.io).
/// </summary>
internal sealed class SmartLockService(HttpClient httpClient, IOptions<SmartLockOptions> options, ILogger<SmartLockService> logger)
    : HttpIntegrationServiceBase(httpClient, logger), ISmartLockService
{
    protected override string IntegrationName => IntegrationNames.SmartLock;

    private string SmartLockId => Uri.EscapeDataString(options.Value.SmartLockId);

    public Task<SmartLockOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        const string operation = "Get overview";

        return ExecuteAsync(operation, async ct =>
        {
            var result = await GetJsonAsync<SmartLockResponse>(operation, $"smartlock/{SmartLockId}", ct);
            var state = result.State ?? throw ResponseError(operation, "The response did not contain the smart lock state.");

            return new SmartLockOverview((SmartLockState)state.State, (SmartLockDoorState)state.DoorState, state.BatteryCharge);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SmartLockLog>> GetActivityLogAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        const string operation = "Get activity log";
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        return ExecuteAsync<IReadOnlyList<SmartLockLog>>(operation, async ct =>
        {
            var result = await GetJsonAsync<List<SmartLockLogResponse>>(operation, $"smartlock/{SmartLockId}/log?limit={limit}", ct);
            return result.Select(x => new SmartLockLog(x.Timestamp.LocalDateTime, (SmartLockAction)x.Action)).ToList();
        }, cancellationToken);
    }

    public Task UnlockAsync(CancellationToken cancellationToken = default)
        => SendAsync("Unlock", HttpMethod.Post, $"smartlock/{SmartLockId}/action/unlock", body: null, cancellationToken);

    private sealed class SmartLockResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("state")]
        public SmartLockStateResponse? State { get; set; }
    }

    private sealed class SmartLockStateResponse
    {
        [JsonPropertyName("state")]
        public int State { get; set; }

        [JsonPropertyName("doorState")]
        public int DoorState { get; set; }

        [JsonPropertyName("batteryCharge")]
        public int BatteryCharge { get; set; }
    }

    private sealed class SmartLockLogResponse
    {
        [JsonPropertyName("date")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonPropertyName("action")]
        public int Action { get; set; }
    }
}
