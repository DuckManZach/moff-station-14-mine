using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Moffstation.Shuttles.Components;
using Content.Shared._Moffstation.Shuttles.Systems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Shuttles.Systems;

/// <summary>
/// Gives every FTL destination map a zone, placed off to one side of whatever the biggest grid on that map is.
/// Maps that ship with a hand-placed zone keep it.
/// </summary>
public sealed partial class FTLZoneSystem : SharedFTLZoneSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    private static readonly EntProtoId ZoneProto = "MoffFTLZone";

    /// <summary>
    /// How far past the edge of the map's largest grid the zone is placed.
    /// </summary>
    private const float MinOffset = 128f;

    private const float MaxOffset = 384f;

    public override void Initialize()
    {
        base.Initialize();

        // Not ComponentStartup - ShuttleConsoleSystem already owns that pair, and the engine allows only one
        // subscription per component+event. MapInit still fires for components added to a live map entity.
        SubscribeLocalEvent<FTLDestinationComponent, MapInitEvent>(OnDestinationMapInit);
        SubscribeLocalEvent<FTLZoneComponent, ComponentStartup>(OnZoneStartup);

        // Broadcast, so subscribing alongside SalvageSystem and NukeopsRuleSystem is fine. Raised only from CanFTL,
        // which nothing but player-driven FTL calls - the emergency shuttle and arrivals go straight to FTLToDock.
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnConsoleFTLAttempt);

        InitializeArrival();
    }

    private void OnConsoleFTLAttempt(ref ConsoleFTLAttemptEvent ev)
    {
        if (InZone(ev.Uid))
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("shuttle-console-ftl-zone");

        // ConsoleFTL discards the reason and the event carries no actor, so tell whoever is flying directly.
        AlertPilots(ev.Uid, ev.Reason);
    }

    private void AlertPilots(EntityUid shuttleUid, string message)
    {
        var query = EntityQueryEnumerator<PilotComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == shuttleUid)
                _popup.PopupEntity(message, uid, uid, PopupType.MediumCaution);
        }
    }

    private void OnZoneStartup(Entity<FTLZoneComponent> ent, ref ComponentStartup args)
    {
        // Consoles draw zones on maps the player is nowhere near, so these have to bypass PVS.
        _pvsOverride.AddGlobalOverride(ent);
    }

    private void OnDestinationMapInit(EntityUid uid, FTLDestinationComponent component, MapInitEvent args)
    {
        EnsureZone(uid, out _);
    }

    /// <summary>
    /// Spawns this map's zone if it hasn't got one and there's a grid to anchor it against. Safe to call repeatedly:
    /// a destination can be registered before its grids exist, so this gets retried when a later stage needs the zone.
    /// </summary>
    public bool EnsureZone(EntityUid mapUid, out Entity<FTLZoneComponent> zone)
    {
        if (TryGetZone(mapUid, out zone))
            return true;

        if (FindReferenceGrid(mapUid) is not { } reference)
            return false;

        var distance = reference.Comp.LocalAABB.Size.Length() / 2f + _random.NextFloat(MinOffset, MaxOffset);
        var position = XformSystem.GetWorldPosition(reference.Owner) + _random.NextAngle().ToVec() * distance;

        // Attached rather than positioned, so it stays parented to the map instead of latching onto a passing grid.
        var uid = SpawnAttachedTo(ZoneProto, new EntityCoordinates(mapUid, position));
        zone = (uid, Comp<FTLZoneComponent>(uid));

        Log.Info($"Placed FTL zone for {ToPrettyString(mapUid)} at {position}, anchored on {ToPrettyString(reference)}");

        return true;
    }

    /// <summary>
    /// The biggest grid on the map, which the zone gets placed relative to.
    /// </summary>
    private Entity<MapGridComponent>? FindReferenceGrid(EntityUid mapUid)
    {
        if (!TryComp<MapComponent>(mapUid, out var map))
            return null;

        Entity<MapGridComponent>? largest = null;
        foreach (var grid in Maps.GetAllGrids(map.MapId))
        {
            if (largest is not { } current ||
                grid.Comp.LocalAABB.Size.Length() > current.Comp.LocalAABB.Size.Length())
            {
                largest = grid;
            }
        }

        return largest;
    }
}
