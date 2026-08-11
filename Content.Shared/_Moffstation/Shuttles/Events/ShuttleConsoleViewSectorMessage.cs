using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Shuttles.Events;

/// <summary>
/// Sent when a pilot looks at a sector on the console. Sectors the player isn't standing in are outside their PVS,
/// so the server has to be told which one to push grids for.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShuttleConsoleViewSectorMessage(NetEntity sector) : BoundUserInterfaceMessage
{
    public NetEntity Sector = sector;
}
