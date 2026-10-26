using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MijnThuis.Integrations.Airconditioning.Gree;

/// <summary>
/// Minimal UDP client implementing the reverse-engineered Gree AC protocol
/// (see https://github.com/tomikaa87/gree-remote). Devices listen on UDP port 7000.
/// Only binding and status/control requests are implemented; discovery is not needed
/// because the device's IP and MAC address are configured.
/// </summary>
internal sealed class GreeClient
{
    private const int DevicePort = 7000;
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };

    /// <summary>
    /// Binds to a device to obtain its device-specific AES key. Tries the legacy ECB scheme first and
    /// falls back to the AES-GCM scheme used by more recent firmware. Returns <c>null</c> when neither works.
    /// </summary>
    public async Task<BoundGreeDevice?> BindAsync(string mac, IPAddress ipAddress, CancellationToken cancellationToken)
    {
        var packJson = JsonSerializer.Serialize(new BindRequestPack { Mac = mac }, JsonOptions);

        foreach (var mode in new[] { GreeEncryptionMode.Ecb, GreeEncryptionMode.Gcm })
        {
            var genericKey = mode == GreeEncryptionMode.Gcm ? GreeCrypto.GenericGcmKey : GreeCrypto.GenericKey;

            try
            {
                var response = await SendAndReceiveAsync(ipAddress, BuildEnvelope(mac, 1, packJson, genericKey, mode), cancellationToken).ConfigureAwait(false);
                var bindResponse = JsonSerializer.Deserialize<BindResponsePack>(DecryptEnvelope(response, genericKey, mode), JsonOptions);

                if (bindResponse is { Result: 200 } && string.Equals(bindResponse.Type, "bindok", StringComparison.OrdinalIgnoreCase))
                {
                    return new BoundGreeDevice { Mac = mac, Key = bindResponse.Key, EncryptionMode = mode };
                }
            }
            catch (Exception ex) when (ex is TimeoutException or CryptographicException or JsonException)
            {
                // No (valid) answer for this encryption mode; try the next one.
            }
        }

        return null;
    }

    /// <summary>
    /// Requests the raw integer values of the given columns (parameters) from a bound device.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> GetStatusAsync(BoundGreeDevice device, IPAddress ipAddress, IEnumerable<string> columns, CancellationToken cancellationToken)
    {
        var packJson = JsonSerializer.Serialize(new StatusRequestPack { Mac = device.Mac, Cols = columns.ToList() }, JsonOptions);
        var response = await SendAndReceiveAsync(ipAddress, BuildEnvelope(device.Mac, 0, packJson, device.Key, device.EncryptionMode), cancellationToken).ConfigureAwait(false);
        var status = JsonSerializer.Deserialize<StatusResponsePack>(DecryptEnvelope(response, device.Key, device.EncryptionMode), JsonOptions);

        if (status is null || status.Type != "dat")
        {
            throw new InvalidDataException("Unexpected status response from the air conditioning unit.");
        }

        return status.Cols
            .Zip(status.Dat)
            .ToDictionary(x => x.First, x => x.Second);
    }

    /// <summary>
    /// Sends one or more parameter changes to a bound device.
    /// </summary>
    public async Task SetParametersAsync(BoundGreeDevice device, IPAddress ipAddress, IReadOnlyDictionary<string, int> parameters, CancellationToken cancellationToken)
    {
        var packJson = JsonSerializer.Serialize(new CommandRequestPack { Opt = parameters.Keys.ToList(), P = parameters.Values.ToList() }, JsonOptions);
        var response = await SendAndReceiveAsync(ipAddress, BuildEnvelope(device.Mac, 0, packJson, device.Key, device.EncryptionMode), cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<CommandResponsePack>(DecryptEnvelope(response, device.Key, device.EncryptionMode), JsonOptions);

        if (result is not { Type: "res", Result: 200 })
        {
            throw new InvalidDataException($"The air conditioning unit rejected the command (result {result?.Result}).");
        }
    }

    private static GreeEnvelope BuildEnvelope(string mac, int i, string packJson, string key, GreeEncryptionMode mode)
    {
        var envelope = new GreeEnvelope { Type = "pack", I = i, Uid = 0, Cid = "app", Tcid = mac };

        if (mode == GreeEncryptionMode.Gcm)
        {
            (envelope.Pack, envelope.Tag) = GreeCrypto.EncryptGcm(packJson, key);
        }
        else
        {
            envelope.Pack = GreeCrypto.Encrypt(packJson, key);
        }

        return envelope;
    }

    private static string DecryptEnvelope(GreeEnvelope envelope, string key, GreeEncryptionMode mode)
    {
        if (envelope.Pack is null)
        {
            throw new InvalidDataException("The response of the air conditioning unit did not contain a 'pack' field.");
        }

        if (mode == GreeEncryptionMode.Gcm)
        {
            return envelope.Tag is null
                ? throw new CryptographicException("Expected a GCM 'tag' field in the response but none was present.")
                : GreeCrypto.DecryptGcm(envelope.Pack, envelope.Tag, key);
        }

        return GreeCrypto.Decrypt(envelope.Pack, key);
    }

    /// <summary>
    /// Sends a single datagram and waits for the reply. Throws <see cref="TimeoutException"/> when the device does not answer.
    /// </summary>
    private static async Task<GreeEnvelope> SendAndReceiveAsync(IPAddress address, GreeEnvelope envelope, CancellationToken cancellationToken)
    {
        using var udp = new UdpClient();
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        DisableConnectionResetReporting(udp.Client);

        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
        await udp.SendAsync(requestBytes, new IPEndPoint(address, DevicePort), cancellationToken).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReceiveTimeout);

        try
        {
            var result = await udp.ReceiveAsync(timeout.Token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GreeEnvelope>(result.Buffer, JsonOptions)
                ?? throw new InvalidDataException("Empty response from the air conditioning unit.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No response from the air conditioning unit at {address} within {ReceiveTimeout.TotalSeconds} seconds.");
        }
        catch (SocketException ex)
        {
            // On Windows an ICMP "port unreachable" reply surfaces as a connection reset.
            throw new TimeoutException($"The air conditioning unit at {address} is not reachable.", ex);
        }
    }

    /// <summary>
    /// On Windows, an ICMP "port unreachable" response causes the next receive to fail with WSAECONNRESET.
    /// This disables that behavior via the SIO_UDP_CONNRESET control code. No-op on other platforms.
    /// </summary>
    private static void DisableConnectionResetReporting(Socket socket)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const int SioUdpConnReset = -1744830452;

        try
        {
            socket.IOControl(SioUdpConnReset, [0], null);
        }
        catch (SocketException)
        {
            // Best-effort; ignore if unsupported.
        }
    }
}
