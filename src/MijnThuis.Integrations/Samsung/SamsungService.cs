using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Network;
using MijnThuis.Integrations.Common.Resilience;
using Polly;
using Polly.Registry;

namespace MijnThuis.Integrations.Samsung;

public interface ISamsungService
{
    Task<bool> IsTheFrameOnAsync(CancellationToken cancellationToken = default);

    Task TurnOnTheFrameAsync(CancellationToken cancellationToken = default);

    Task TurnOffTheFrameAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Remembers the remote control token of the TV, so it only needs to be requested (and accepted on the TV) once per process.
/// </summary>
public interface ISamsungTokenStore
{
    string? Token { get; set; }
}

internal sealed class SamsungTokenStore(IOptions<SamsungOptions> options) : ISamsungTokenStore
{
    private string? _token;

    public string? Token
    {
        get => string.IsNullOrWhiteSpace(_token) ? options.Value.Token : _token;
        set => _token = value;
    }
}

/// <summary>
/// Samsung The Frame TV through its local remote control API (REST on port 8001, secure WebSocket on port 8002).
/// </summary>
internal sealed class SamsungService(
    HttpClient httpClient,
    IWakeOnLanSender wakeOnLanSender,
    ISamsungTokenStore tokenStore,
    ResiliencePipelineProvider<string> pipelineProvider,
    IOptions<SamsungOptions> options,
    TimeProvider timeProvider,
    ILogger<SamsungService> logger) : IntegrationServiceBase(logger), ISamsungService
{
    private static readonly TimeSpan KeyPressDuration = TimeSpan.FromSeconds(5);

    private readonly ResiliencePipeline _pipeline = pipelineProvider.GetPipeline(IntegrationResiliencePipelines.LocalDevice);

    protected override string IntegrationName => IntegrationNames.Samsung;

    public Task<bool> IsTheFrameOnAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Get status", async ct =>
        {
            var url = $"http://{options.Value.IpAddress}:8001/api/v2/";

            try
            {
                using var response = await httpClient.GetAsync(url, ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // The TV doesn't answer at all when it is turned off (or in art mode standby).
                Logger.LogDebug(ex, "The Frame did not respond, assuming it is turned off");
                return false;
            }
        }, cancellationToken);
    }

    public Task TurnOnTheFrameAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Turn on", async ct =>
        {
            var settings = options.Value;
            await wakeOnLanSender.SendMagicPacketAsync(settings.MacAddress, settings.IpAddress, ct);

            await Task.Delay(settings.BootDelay, timeProvider, ct);

            // Press the return key to hide the home menu.
            await PressKeyAsync("Click", "KEY_RETURN", ct);
        }, cancellationToken);
    }

    public Task TurnOffTheFrameAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Turn off", async ct =>
        {
            // Keep the power key pressed to fully turn off the TV instead of switching to art mode.
            await PressKeyAsync("Press", "KEY_POWER", ct);
            await Task.Delay(KeyPressDuration, timeProvider, ct);
            await PressKeyAsync("Release", "KEY_POWER", ct);
        }, cancellationToken);
    }

    private async Task PressKeyAsync(string command, string key, CancellationToken cancellationToken)
    {
        var token = tokenStore.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = await GetTokenAsync(cancellationToken);
            tokenStore.Token = token;
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(new RemoteControlMessage
        {
            Parameters = new RemoteControlParameters { Command = command, Key = key }
        });

        await _pipeline.ExecuteAsync(async ct =>
        {
            using var webSocket = await ConnectAsync(token, ct);
            await webSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, ct);
            await CloseAsync(webSocket, ct);
        }, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        // The first message on a new connection contains the token (after the connection is allowed on the TV).
        return await _pipeline.ExecuteAsync(async ct =>
        {
            using var webSocket = await ConnectAsync(token: null, ct);

            var buffer = new byte[4096];
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(buffer, ct);
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

            await CloseAsync(webSocket, ct);

            var response = JsonSerializer.Deserialize<TokenMessage>(message.ToArray());
            var token = response?.Data?.Token;

            return string.IsNullOrWhiteSpace(token)
                ? throw ResponseError("Get token", $"The TV did not return a token (event '{response?.Event ?? "unknown"}'). Allow the connection on the TV and try again.")
                : token;
        }, cancellationToken);
    }

    private async Task<ClientWebSocket> ConnectAsync(string? token, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var appName = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.AppName));
        var url = $"wss://{settings.IpAddress}:8002/api/v2/channels/samsung.remote.control?name={Uri.EscapeDataString(appName)}";
        if (!string.IsNullOrWhiteSpace(token))
        {
            url += $"&token={Uri.EscapeDataString(token)}";
        }

        var webSocket = new ClientWebSocket();

        // The TV uses a self-signed certificate.
        webSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        try
        {
            await webSocket.ConnectAsync(new Uri(url), cancellationToken);
            return webSocket;
        }
        catch
        {
            webSocket.Dispose();
            throw;
        }
    }

    private static async Task CloseAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
        }
    }

    private sealed class RemoteControlMessage
    {
        [JsonPropertyName("method")]
        public string Method { get; init; } = "ms.remote.control";

        [JsonPropertyName("params")]
        public required RemoteControlParameters Parameters { get; init; }
    }

    private sealed class RemoteControlParameters
    {
        [JsonPropertyName("Cmd")]
        public required string Command { get; init; }

        [JsonPropertyName("DataOfCmd")]
        public required string Key { get; init; }

        [JsonPropertyName("Option")]
        public string Option { get; init; } = "false";

        [JsonPropertyName("TypeOfRemote")]
        public string TypeOfRemote { get; init; } = "SendRemoteKey";
    }

    private sealed class TokenMessage
    {
        [JsonPropertyName("event")]
        public string? Event { get; set; }

        [JsonPropertyName("data")]
        public TokenData? Data { get; set; }
    }

    private sealed class TokenData
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }
}
