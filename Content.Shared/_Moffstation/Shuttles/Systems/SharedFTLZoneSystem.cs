using Content.Shared._Moffstation.Shuttles.Components;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Moffstation.Shuttles.Systems;

/// <summary>
/// Queries for <see cref="FTLZoneComponent"/>. Shared so the console UI can grey out FTL for the same reason the
/// server refuses it.
/// </summary>
public abstract partial class SharedFTLZoneSystem : EntitySystem
{
    [Dependency] protected SharedMapSystem Maps = default!;
    [Dependency] protected SharedTransformSystem XformSystem = default!;

    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    /// <summary>
    /// The zone entity on the given map, if it has one.
    /// </summary>
    public bool TryGetZone(EntityUid mapUid, out Entity<FTLZoneComponent> zone)
    {
        zone = default;

        var query = EntityQueryEnumerator<FTLZoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            zone = (uid, comp);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether this shuttle is sitting inside its own map's zone, and so may FTL out.
    /// </summary>
    public bool InZone(EntityUid shuttleUid)
    {
        var xform = Transform(shuttleUid);

        // A map without a zone is unrestricted.
        if (xform.MapUid is not { } mapUid || !TryGetZone(mapUid, out var zone))
            return true;

        if (!_physicsQuery.TryComp(shuttleUid, out var physics))
            return false;

        var shuttlePos = Maps.GetGridPosition((shuttleUid, physics, xform));

        return (shuttlePos - XformSystem.GetWorldPosition(zone)).Length() <= zone.Comp.Radius;
    }
}
