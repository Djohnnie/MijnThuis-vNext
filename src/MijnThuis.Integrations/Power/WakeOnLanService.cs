using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MijnThuis.Integrations.Common;
using MijnThuis.Integrations.Common.Network;

namespace MijnThuis.Integrations.Power;

public interface IWakeOnLanService
{
    /// <summary>
    /// Wakes the configured computer by sending a Wake-on-LAN magic packet.
    /// </summary>
    Task WakeAsync(CancellationToken cancellationToken = default);
}

internal sealed class WakeOnLanService(IWakeOnLanSender sender, IOptions<WakeOnLanOptions> options, ILogger<WakeOnLanService> logger)
    : IntegrationServiceBase(logger), IWakeOnLanService
{
    protected override string IntegrationName => IntegrationNames.WakeOnLan;

    public Task WakeAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Wake", ct =>
        {
            var settings = options.Value;
            return sender.SendMagicPacketAsync(settings.MacAddress, settings.IpAddress, ct);
        }, cancellationToken);
    }
}
