using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Moffstation.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Moffstation.Shuttles.Systems;

/// <summary>
/// Drives the shuttle console's sector list: pushes a sector's grids to whoever is looking at it, and handles the
/// jump when they commit. Owns its own BUI messages, so <c>ShuttleConsoleSystem</c> needs no edit.
/// </summary>
public sealed partial class SectorConsoleSystem : EntitySystem
{
    [Dependency] private FTLZoneSystem _zone = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    /// <summary>
    /// Grids force-sent to each session, so they can be dropped again when that player looks elsewhere.
    /// </summary>
    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _viewing = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key,
            subs =>
            {
                subs.Event<ShuttleConsoleViewSectorMessage>(OnViewSector);
                subs.Event<ShuttleConsoleFTLSectorMessage>(OnSectorFTL);
            });

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        // PvsOverrideSystem drops its own session state on disconnect; this just stops us holding the session.
        if (args.NewStatus == SessionStatus.Disconnected)
            _viewing.Remove(args.Session);
    }

    /// <summary>
    /// Pushes the viewed sector's grids to this player. Only one sector is in flight per player, so the cost scales
    /// with people looking at a console rather than with how many maps exist.
    /// </summary>
    private void OnViewSector(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleViewSectorMessage args)
    {
        if (!_player.TryGetSessionByEntity(args.Actor, out var session))
            return;

        var sent = _viewing.GetOrNew(session);

        foreach (var grid in sent)
        {
            _pvsOverride.RemoveForceSend(grid, session);
        }

        sent.Clear();

        if (!TryGetEntity(args.Sector, out var mapUid) || !TryComp<MapComponent>(mapUid, out var map))
            return;

        foreach (var grid in _maps.GetAllGrids(map.MapId))
        {
            // Same filter the radar uses to decide what's worth drawing: skips debris and IFF-hidden grids.
            if (!_shuttle.CanDraw(grid))
                continue;

            // ForceSend rather than AddGlobalOverride: this sends the grid and its parents but NOT its children,
            // so the outline crosses the wire without every entity aboard it.
            _pvsOverride.AddForceSend(grid, session);
            sent.Add(grid);
        }
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
    /// Where to aim the jump. For a zone map this only has to land on the right map - <see cref="FTLZoneSystem"/>'s
    /// FTLRequestEvent hook replaces it with a sampled point inside the zone - so don't pick a spot here or it gets
    /// computed twice and thrown away.
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

        if (!_zone.EnsureZone(mapUid, out var zone))
            return false;

        target = new EntityCoordinates(mapUid, _transform.GetWorldPosition(zone));
        return true;
    }
}
