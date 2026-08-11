using System.Numerics;
using Content.Shared._Moffstation.Shuttles.Components;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Moffstation.Shuttles.Systems;

/// <summary>
/// Queries for <see cref="FTLZoneComponent"/>. Shared so the console UI can grey out FTL for the same reason the
/// server refuses it.
/// </summary>
public abstract class SharedFTLZoneSystem : EntitySystem
{
    [Dependency] protected SharedMapSystem Maps = default!;
    [Dependency] protected SharedTransformSystem XformSystem = default!;

    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    public bool TryGetZone(EntityUid mapUid, out Vector2 origin, out float radius)
    {
        origin = default;
        radius = 0f;

        if (!TryComp<FTLZoneComponent>(mapUid, out var zone) || ResolveOrigin((mapUid, zone)) is not { } resolved)
            return false;

        origin = resolved;
        radius = zone.Radius;
        return true;
    }

    /// <summary>
    /// The server generates a missing origin on demand; the client only ever reads what it was sent.
    /// </summary>
    protected virtual Vector2? ResolveOrigin(Entity<FTLZoneComponent> zone) => zone.Comp.Origin;

    /// <summary>
    /// Whether this shuttle is sitting inside its own map's zone, and so may FTL out.
    /// </summary>
    public bool InZone(EntityUid shuttleUid)
    {
        var xform = Transform(shuttleUid);

        // A map without a zone is unrestricted.
        if (xform.MapUid is not { } mapUid || !TryGetZone(mapUid, out var origin, out var radius))
            return true;

        if (!_physicsQuery.TryComp(shuttleUid, out var physics))
            return false;

        return (Maps.GetGridPosition((shuttleUid, physics, xform)) - origin).Length() <= radius;
    }
}
