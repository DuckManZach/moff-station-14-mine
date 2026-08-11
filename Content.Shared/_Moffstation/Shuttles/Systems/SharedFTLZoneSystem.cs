using Content.Shared._Moffstation.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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

    [Dependency] protected EntityQuery<PhysicsComponent> PhysicsQuery = default!;

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
    /// The map's biggest grid - its "station". Both the zone placement and the console's camera anchor on this, so
    /// they have to agree on which grid it is.
    /// </summary>
    public Entity<MapGridComponent>? GetLargestGrid(MapId mapId)
    {
        Entity<MapGridComponent>? largest = null;

        foreach (var grid in Maps.GetAllGrids(mapId))
        {
            if (largest is not { } current ||
                grid.Comp.LocalAABB.Size.Length() > current.Comp.LocalAABB.Size.Length())
            {
                largest = grid;
            }
        }

        return largest;
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

        if (!PhysicsQuery.TryComp(shuttleUid, out var physics))
            return false;

        var shuttlePos = Maps.GetGridPosition((shuttleUid, physics, xform));

        return (shuttlePos - XformSystem.GetWorldPosition(zone)).Length() <= zone.Comp.Radius;
    }
}
