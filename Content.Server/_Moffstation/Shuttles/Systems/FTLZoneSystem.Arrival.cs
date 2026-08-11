using System.Numerics;
using Content.Server.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;

namespace Content.Server._Moffstation.Shuttles.Systems;

/// <summary>
/// Redirects incoming FTLs so they land inside the destination map's zone.
/// </summary>
public sealed partial class FTLZoneSystem
{
    /// <summary>
    /// How many spots inside the zone to try before settling for the middle of it.
    /// </summary>
    private const int ArrivalAttempts = 10;

    // Not readonly - FindGridsIntersecting takes it by ref.
    private List<Entity<MapGridComponent>> _grids = new();

    private void InitializeArrival()
    {
        // Only FTLToCoordinates raises this; FTLToDock sets its own target and never does, so dock FTLs - the
        // emergency shuttle, arrivals, salvage recall - keep landing on their dock untouched.
        SubscribeLocalEvent<FTLComponent, FTLRequestEvent>(OnFTLRequest);
    }

    private void OnFTLRequest(Entity<FTLComponent> ent, ref FTLRequestEvent args)
    {
        if (!TryGetArrival(ent.Owner, args.MapUid, out var coordinates, out var angle))
            return;

        ent.Comp.TargetCoordinates = coordinates;
        ent.Comp.TargetAngle = angle;
    }

    /// <summary>
    /// Picks a free spot inside the destination's zone for this shuttle to arrive at.
    /// </summary>
    public bool TryGetArrival(EntityUid shuttleUid, EntityUid mapUid, out EntityCoordinates coordinates, out Angle angle)
    {
        coordinates = default;
        angle = default;

        // Beacon maps (salvage expeditions) have their own arrival point already.
        if (_shuttle.IsBeaconMap(mapUid) ||
            !TryComp<MapComponent>(mapUid, out var map) ||
            !EnsureZone(mapUid, out var zone))
        {
            return false;
        }

        var origin = XformSystem.GetWorldPosition(zone);

        // Keep the whole shuttle inside the zone, not just its centre.
        var spread = MathF.Max(0f, zone.Comp.Radius - _shuttle.GetFTLBufferRange(shuttleUid));

        angle = _random.NextAngle();

        var centre = origin;
        for (var i = 0; i < ArrivalAttempts; i++)
        {
            // sqrt keeps the sampling even across the disc instead of clumping in the middle.
            var candidate = origin + _random.NextAngle().ToVec() * (spread * MathF.Sqrt(_random.NextFloat()));

            if (!IsClear(shuttleUid, map.MapId, candidate))
                continue;

            centre = candidate;
            break;
        }

        coordinates = new EntityCoordinates(mapUid, centre);

        // Arrival sets the grid's origin, but "inside the zone" is measured from its centre of mass, so undo the
        // offset the same way ConsoleFTL does. Otherwise a shuttle can arrive and immediately be unable to leave.
        if (PhysicsQuery.TryComp(shuttleUid, out var physics))
            coordinates = coordinates.Offset(angle.RotateVec(-physics.LocalCenter));

        return true;
    }

    /// <summary>
    /// Whether the shuttle would fit here without overlapping another grid. This is the grid half of
    /// <see cref="SharedShuttleSystem.FTLFree"/>, minus its range check, which no sector-to-sector jump could pass.
    /// </summary>
    private bool IsClear(EntityUid shuttleUid, MapId mapId, Vector2 position)
    {
        var buffer = _shuttle.GetFTLBufferRange(shuttleUid) + SharedShuttleSystem.FTLBufferRange;

        _grids.Clear();
        Maps.FindGridsIntersecting(mapId,
            new PhysShapeCircle(buffer, position),
            Robust.Shared.Physics.Transform.Empty,
            ref _grids,
            includeMap: false);

        foreach (var grid in _grids)
        {
            if (grid.Owner != shuttleUid)
                return false;
        }

        return true;
    }
}
