using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._Moffstation.Shuttles.Components;
using Robust.Shared.Map.Components;

namespace Content.Shared._Moffstation.Shuttles.Systems;

public abstract partial class SharedHyperspaceRiftSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;

    public bool TryGetRift(EntityUid? mapUid, out Entity<HyperspaceRiftComponent, TransformComponent> rift)
    {
        rift = default;

        if (mapUid == null)
            return false;

        foreach (var ent in AllEntityQuery<HyperspaceRiftComponent, TransformComponent>())
        {
            if (ent.Comp2.MapUid != mapUid)
                continue;

            rift = ent;
            return true;
        }

        return false;
    }

    /// <summary>
    /// True if the shuttle's map has no rift, or the shuttle is inside it.
    /// </summary>
    public bool CanDepart(EntityUid shuttle)
    {
        return !TryGetRift(Transform(shuttle).MapUid, out var rift) || IsInRift(shuttle, rift);
    }

    public bool IsInRift(EntityUid shuttle, Entity<HyperspaceRiftComponent, TransformComponent> rift)
    {
        if (Transform(shuttle).MapUid != rift.Comp2.MapUid)
            return false;

        var distance = GetShuttleCentre(shuttle) - _transform.GetWorldPosition(rift.Comp2);
        return distance.Length() <= rift.Comp1.Radius;
    }

    /// <summary>
    /// The world position of the rift on a map, if it has one.
    /// </summary>
    public bool TryGetRiftPosition(EntityUid? mapUid, [NotNullWhen(true)] out Vector2? position)
    {
        position = null;

        if (!TryGetRift(mapUid, out var rift))
            return false;

        position = _transform.GetWorldPosition(rift.Comp2);
        return true;
    }

    private Vector2 GetShuttleCentre(EntityUid shuttle)
    {
        if (!_gridQuery.TryComp(shuttle, out var grid))
            return _transform.GetWorldPosition(shuttle);

        return Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(shuttle));
    }
}
