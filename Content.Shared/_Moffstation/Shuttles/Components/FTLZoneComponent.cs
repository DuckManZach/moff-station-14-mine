using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Moffstation.Shuttles.Components;

/// <summary>
/// A circular region on the attached map entity. Shuttles must be inside their own map's zone to FTL out, and
/// console FTLs into this map arrive inside it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FTLZoneComponent : Component
{
    /// <summary>
    /// Centre of the zone in map coordinates. Null until it has been resolved against the map's largest grid.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Vector2? Origin;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float Radius = 96f;

    /// <summary>
    /// How far past the edge of the reference grid the zone is placed.
    /// </summary>
    [DataField]
    public float MinOffset = 128f;

    [DataField]
    public float MaxOffset = 384f;
}
