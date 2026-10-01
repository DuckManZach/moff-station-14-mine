using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Shuttles.Events;

/// <summary>
/// Raised on a client when it wishes to FTL to another map's hyperspace rift.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShuttleConsoleFTLSectorMessage(NetEntity map) : BoundUserInterfaceMessage
{
    public NetEntity Map { get; } = map;
}
