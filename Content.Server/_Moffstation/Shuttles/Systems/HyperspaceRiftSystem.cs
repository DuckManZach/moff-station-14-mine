using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Moffstation.Shuttles.Components;
using Content.Shared._Moffstation.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Shuttles.Systems;

public sealed partial class HyperspaceRiftSystem : SharedHyperspaceRiftSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<FTLMapComponent> _ftlMapQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    private static readonly EntProtoId<HyperspaceRiftComponent> RiftPrototype = "HyperspaceRift";

    private const int SpawnAttempts = 10;
    private const int ArrivalAttempts = 20;

    private List<Entity<MapGridComponent>> _grids = new();

    [SubscribeLocalEvent]
    private void OnDestinationMapInit(Entity<FTLDestinationComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.BeaconsOnly || _ftlMapQuery.HasComp(ent) || !_mapQuery.HasComp(ent))
            return;

        // Spawning onto a map while it is still being initialized would modify its children mid-iteration.
        QueueLocalEvent(new SpawnHyperspaceRiftEvent(ent));
    }

    [SubscribeLocalEvent]
    private void OnSpawnHyperspaceRift(SpawnHyperspaceRiftEvent args)
    {
        if (TerminatingOrDeleted(args.Map) || TryGetRift(args.Map, out _))
            return;

        var rift = Spawn(RiftPrototype, new EntityCoordinates(args.Map, Vector2.Zero));
        _transform.SetWorldPosition(rift, GetRiftPosition(args.Map, Comp<HyperspaceRiftComponent>(rift)));
    }

    [SubscribeLocalEvent]
    private void OnRiftMapInit(Entity<HyperspaceRiftComponent> ent, ref MapInitEvent args)
    {
        // Clients need every rift to show sectors' rifts and check departure.
        _pvsOverride.AddGlobalOverride(ent);
    }

    [SubscribeLocalEvent]
    private void OnConsoleFTLAttempt(ref ConsoleFTLAttemptEvent args)
    {
        if (args.Cancelled || CanDepart(args.Uid))
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("shuttle-console-outside-rift");
    }

    /// <summary>
    /// Picks where a shuttle FTLing to a map arrives: inside the map's rift, at its beacon, or near its largest grid.
    /// </summary>
    public bool TryGetArrival(EntityUid shuttle, EntityUid mapUid, Angle angle, out EntityCoordinates coordinates)
    {
        coordinates = EntityCoordinates.Invalid;

        if (!_physicsQuery.TryComp(shuttle, out var physics))
            return false;

        if (TryGetRift(mapUid, out var rift))
        {
            if (!TryGetRiftArrival(shuttle, rift, out var position))
                return false;

            coordinates = new EntityCoordinates(mapUid, position).Offset(angle.RotateVec(-physics.LocalCenter));
            return true;
        }

        if (_shuttle.IsBeaconMap(mapUid))
        {
            foreach (var beacon in AllEntityQuery<FTLBeaconComponent, TransformComponent>())
            {
                if (beacon.Comp2.ParentUid != mapUid)
                    continue;

                coordinates = new EntityCoordinates(mapUid, beacon.Comp2.LocalPosition).Offset(angle.RotateVec(-physics.LocalCenter));
                return true;
            }

            return false;
        }

        // Targeting a grid makes FTL arrival find a free spot near it.
        coordinates = GetLargestGrid(mapUid) is { } grid
            ? new EntityCoordinates(grid, Vector2.Zero)
            : new EntityCoordinates(mapUid, Vector2.Zero);
        return true;
    }

    private bool TryGetRiftArrival(EntityUid shuttle, Entity<HyperspaceRiftComponent, TransformComponent> rift, out Vector2 position)
    {
        var centre = _transform.GetWorldPosition(rift.Comp2);
        var buffer = _shuttle.GetFTLBufferRange(shuttle);
        var radius = rift.Comp1.Radius;

        // Fall back to just outside the rift if it is too crowded to fit the shuttle.
        for (var i = 0; i < ArrivalAttempts * 2; i++)
        {
            position = i < ArrivalAttempts
                ? centre + _random.NextVector2(MathF.Max(0f, radius - buffer))
                : centre + _random.NextVector2(radius, radius * 2f);

            if (IsSpotFree(shuttle, rift.Comp2.MapID, position, buffer + SharedShuttleSystem.FTLBufferRange))
                return true;
        }

        position = default;
        return false;
    }

    private bool IsSpotFree(EntityUid shuttle, MapId mapId, Vector2 position, float range)
    {
        foreach (var exclusion in AllEntityQuery<FTLExclusionComponent, TransformComponent>())
        {
            if (!exclusion.Comp1.Enabled || exclusion.Comp2.MapID != mapId)
                continue;

            if ((_transform.GetWorldPosition(exclusion.Comp2) - position).Length() <= exclusion.Comp1.Range + range)
                return false;
        }

        _grids.Clear();
        _map.FindGridsIntersecting(mapId, Box2.CenteredAround(position, new Vector2(range * 2f)), ref _grids, approx: true, includeMap: false);

        foreach (var grid in _grids)
        {
            if (grid.Owner != shuttle)
                return false;
        }

        return true;
    }

    private Vector2 GetRiftPosition(EntityUid mapUid, HyperspaceRiftComponent rift)
    {
        if (GetLargestGrid(mapUid) is not { } grid)
            return Vector2.Zero;

        var mapId = Transform(grid).MapID;
        var bounds = _transform.GetWorldMatrix(grid).TransformBox(Comp<MapGridComponent>(grid).LocalAABB);
        var halfExtent = bounds.Size.Length() / 2f;
        var position = Vector2.Zero;

        for (var i = 0; i < SpawnAttempts; i++)
        {
            var distance = halfExtent + _random.NextFloat(rift.SpawnDistance.Min, rift.SpawnDistance.Max);
            position = bounds.Center + _random.NextAngle().ToVec() * distance;

            _grids.Clear();
            _map.FindGridsIntersecting(mapId, Box2.CenteredAround(position, new Vector2(rift.Radius * 2f)), ref _grids, approx: true, includeMap: false);

            if (_grids.Count == 0)
                break;
        }

        return position;
    }

    /// <summary>
    /// The station's largest grid on this map, otherwise the map's largest grid.
    /// </summary>
    private EntityUid? GetLargestGrid(EntityUid mapUid)
    {
        var mapId = Transform(mapUid).MapID;

        if (_station.GetStationInMap(mapId) is { } station &&
            _station.GetLargestGrid(station) is { } stationGrid &&
            Transform(stationGrid).MapID == mapId)
        {
            return stationGrid;
        }

        EntityUid? largest = null;
        var largestArea = 0f;

        foreach (var grid in _map.GetAllGrids(mapId))
        {
            var area = grid.Comp.LocalAABB.Width * grid.Comp.LocalAABB.Height;
            if (area <= largestArea)
                continue;

            largest = grid.Owner;
            largestArea = area;
        }

        return largest;
    }
}

public sealed class SpawnHyperspaceRiftEvent(EntityUid map) : EntityEventArgs
{
    public readonly EntityUid Map = map;
}
