using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Shuttles.Events;

/// <summary>
/// Sent when a pilot picks a sector to FTL to. The destination is a map entity; where on it the shuttle ends up is
/// the server's call, not the pilot's.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShuttleConsoleFTLSectorMessage(NetEntity destination) : BoundUserInterfaceMessage
{
    public NetEntity Destination = destination;
}
