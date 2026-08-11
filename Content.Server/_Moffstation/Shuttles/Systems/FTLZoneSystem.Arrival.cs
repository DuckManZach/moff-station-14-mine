using System.Numerics;
using Content.Server.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
    private bool TryGetArrival(EntityUid shuttleUid, EntityUid mapUid, out EntityCoordinates coordinates, out Angle angle)
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

        List<ShuttleExclusionObject>? exclusions = null;
        _console.GetFTLExclusions(ref exclusions);

        var centre = origin;
        for (var i = 0; i < ArrivalAttempts; i++)
        {
            // sqrt keeps the sampling even across the disc instead of clumping in the middle.
            var candidate = origin + _random.NextAngle().ToVec() * (spread * MathF.Sqrt(_random.NextFloat()));

            if (!_shuttle.FTLFree(shuttleUid, new EntityCoordinates(mapUid, candidate), angle, exclusions, checkRange: false))
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
}
