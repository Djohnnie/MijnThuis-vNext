using System.Net;
using System.Net.Sockets;

namespace MijnThuis.Integrations.Common.Network;

/// <summary>
/// Sends Wake-on-LAN "magic packets". Shared by every integration that needs to wake up a device.
/// </summary>
public interface IWakeOnLanSender
{
    /// <param name="macAddress">MAC address of the device, in any common hex notation (AA:BB:.., AA-BB-.., AABB..).</param>
    /// <param name="ipAddress">Target (usually broadcast) address the packet is sent to on UDP port 9.</param>
    Task SendMagicPacketAsync(string macAddress, string ipAddress, CancellationToken cancellationToken = default);
}

internal sealed class WakeOnLanSender : IWakeOnLanSender
{
    private const int WakeOnLanPort = 9;
    private const int MacAddressLength = 6;
    private const int MacAddressRepetitions = 16;

    public async Task SendMagicPacketAsync(string macAddress, string ipAddress, CancellationToken cancellationToken = default)
    {
        var target = new IPEndPoint(IPAddress.Parse(ipAddress), WakeOnLanPort);
        var magicPacket = BuildMagicPacket(macAddress);

        using var client = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
        await client.SendAsync(magicPacket, target, cancellationToken).ConfigureAwait(false);
    }

    internal static byte[] BuildMagicPacket(string macAddress)
    {
        var hex = macAddress.Replace(":", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        var macBytes = Convert.FromHexString(hex);

        if (macBytes.Length != MacAddressLength)
        {
            throw new FormatException($"'{macAddress}' is not a valid MAC address.");
        }

        // 6 x 0xFF followed by the MAC address repeated 16 times.
        var packet = new byte[MacAddressLength * (MacAddressRepetitions + 1)];
        packet.AsSpan(0, MacAddressLength).Fill(0xFF);

        for (var i = 1; i <= MacAddressRepetitions; i++)
        {
            macBytes.CopyTo(packet, i * MacAddressLength);
        }

        return packet;
    }
}
