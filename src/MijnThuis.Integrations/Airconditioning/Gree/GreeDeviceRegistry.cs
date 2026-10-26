using System.Collections.Concurrent;

namespace MijnThuis.Integrations.Airconditioning.Gree;

/// <summary>
/// Keeps the device-specific keys of bound Gree devices for the lifetime of the application,
/// so we don't need to re-bind before every status/control request. Registered as a singleton.
/// </summary>
internal sealed class GreeDeviceRegistry
{
    private readonly ConcurrentDictionary<string, BoundGreeDevice> _devices = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string mac, out BoundGreeDevice device) => _devices.TryGetValue(mac, out device!);

    public void Set(BoundGreeDevice device) => _devices[device.Mac] = device;

    public void Remove(string mac) => _devices.TryRemove(mac, out _);
}
