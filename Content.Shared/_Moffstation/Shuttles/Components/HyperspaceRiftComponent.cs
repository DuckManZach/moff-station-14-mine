using Content.Shared._Moffstation.Shuttles.Systems;
using Content.Shared.Destructible.Thresholds;

namespace Content.Shared._Moffstation.Shuttles.Components;

/// <summary>
/// A shuttle must be within <see cref="Radius"/> of the rift on its map to FTL, and arrives within the destination map's rift.
/// </summary>
[RegisterComponent, Access(typeof(SharedHyperspaceRiftSystem))]
public sealed partial class HyperspaceRiftComponent : Component
{
    [DataField]
    public float Radius = 80f;

    /// <summary>
    /// How far from the edge of the map's largest grid the rift is placed.
    /// </summary>
    [DataField]
    public MinMax SpawnDistance = new(150f, 300f);
}
