using MijnThuis.Integrations.Common.Network;

namespace MijnThuis.Integrations.Tests;

public class WakeOnLanSenderTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("AABBCCDDEEFF")]
    public void BuildMagicPacket_CreatesSyncStreamFollowedBySixteenMacRepetitions(string macAddress)
    {
        var packet = WakeOnLanSender.BuildMagicPacket(macAddress);

        Assert.Equal(102, packet.Length);
        Assert.All(packet.Take(6), b => Assert.Equal(0xFF, b));
        for (var i = 1; i <= 16; i++)
        {
            Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }, packet.Skip(i * 6).Take(6));
        }
    }

    [Theory]
    [InlineData("AA:BB:CC:DD:EE")]
    [InlineData("not-a-mac")]
    public void BuildMagicPacket_RejectsInvalidMacAddresses(string macAddress)
    {
        Assert.ThrowsAny<FormatException>(() => WakeOnLanSender.BuildMagicPacket(macAddress));
    }
}
