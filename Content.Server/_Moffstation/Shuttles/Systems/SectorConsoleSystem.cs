using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Moffstation.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Moffstation.Shuttles.Systems;

/// <summary>
/// Handles the shuttle console's sector list jumping. Owns its own BUI message, so <c>ShuttleConsoleSystem</c> needs
/// no edit.
/// </summary>
public sealed partial class SectorConsoleSystem : EntitySystem
{
    [Dependency] private FTLZoneSystem _zone = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key,
            subs => subs.Event<ShuttleConsoleFTLSectorMessage>(OnSectorFTL));
    }

    private void OnSectorFTL(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleFTLSectorMessage args)
    {
        if (!TryGetEntity(args.Destination, out var mapUid) || !TryComp<MapComponent>(mapUid, out var map))
            return;

        if (Transform(ent).GridUid is not { } shuttleUid ||
            !TryComp(shuttleUid, out ShuttleComponent? shuttleComp) ||
            !shuttleComp.Enabled)
        {
            return;
        }

        // Raises ConsoleFTLAttemptEvent, which is where the zone departure gate and its pilot popup live.
        if (!_shuttle.CanFTL(shuttleUid, out _) ||
            !_shuttle.CanFTLTo(shuttleUid, map.MapId, ent) ||
            !TryGetSectorTarget(mapUid.Value, out var target))
        {
            return;
        }

        _shuttle.FTLToCoordinates(shuttleUid, shuttleComp, target, Angle.Zero);
    }

    /// <summary>
    /// Where to aim the jump. A zone map only has to land on the right map - <see cref="FTLZoneSystem"/>'s
    /// FTLRequestEvent hook replaces the target with a sampled point inside the zone.
    /// </summary>
    private bool TryGetSectorTarget(EntityUid mapUid, out EntityCoordinates target)
    {
        target = default;

        // Beacon-only maps (salvage expeditions) keep their fixed arrival point, and the zone hook skips them, so
        // this has to be checked first or they'd be dumped at the zone instead of the dungeon.
        if (_shuttle.IsBeaconMap(mapUid))
        {
            var beacons = EntityQueryEnumerator<FTLBeaconComponent, TransformComponent>();
            while (beacons.MoveNext(out _, out _, out var beaconXform))
            {
                if (beaconXform.MapUid != mapUid)
                    continue;

                target = new EntityCoordinates(mapUid, _transform.GetWorldPosition(beaconXform));
                return true;
            }

            return false;
        }

        if (!_zone.EnsureZone(mapUid, out _))
            return false;

        target = new EntityCoordinates(mapUid, Vector2.Zero);
        return true;
    }
}
