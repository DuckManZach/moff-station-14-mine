using Content.Server._CD.Spawners;
using Content.Server._Moffstation.Objectives.Components;
using Content.Server._Moffstation.Objectives.Systems;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Server.Storage.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Station.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Stowaway;

public sealed class StowawaySystem : StationEventSystem<StowawayComponent>
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AntagPreferenceSelectionSystem _antagPref = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    protected override void Ended(EntityUid uid, StowawayComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)  // Moffstation - Changed to use the upstream component
    {
        base.Ended(uid, component, gameRule, args);

        var validLockers = new List<Entity<EntityStorageComponent>>();

        //Get the station
        if (!TryGetRandomStation(out var station) ||
            !TryComp<StationDataComponent>(station, out var data))
        {
            Log.Warning($"Unable to find a valid station for {args.RuleId}!");
            ForceEndSelf(uid, gameRule);
            return;
        }

        // Get the map that the main station exists on
        if (_stationSystem.GetLargestGrid(station.Value) is not { } largestGrid)
        {
            Log.Warning($"Unable to find map for GameRule {args.RuleId}!");
            ForceEndSelf(uid, gameRule);
            return;
        }
        var map = Transform(largestGrid).MapID;
        if (map == MapId.Nullspace)
        {
            Log.Warning($"Attempted to load into nullspace for GameRule {args.RuleId}!");
            ForceEndSelf(uid, gameRule);
            return;
        }

        if (!TryComp<AntagPreferenceSelectionComponent>(uid, out var antagPref))
        {
            Log.Warning($"Unable to find a valid station for {args.RuleId}!");
            ForceEndSelf(uid, gameRule);
            return;
        }

        var stowaways = new HashSet<EntityUid>();
        for (var i = 0; i < component.Players; i++)
        {
            stowaways.Add(_antagPref.SelectTarget((uid, antagPref), _mind.GetAliveHumans()));
        }

        var query = EntityQueryEnumerator<EntityStorageComponent, TransformComponent>();
        while (query.MoveNext(out var locker, out var storage, out var xform))
        {
            if (_stationSystem.GetOwningStation(uid, xform) != station)
                continue;

            if (!_entityStorage.CanInsert(locker, uid, storage))
                continue;

            validLockers.Add((uid, storage));
        }

        foreach (var stowaway in stowaways)
        {
            var locker = new Entity<EntityStorageComponent>();
            var attempts = 0;
            while (!_entityStorage.CanInsert(stowaway, locker) && attempts < component.MaxAttempts)
            {
                locker = _random.Pick(validLockers);
                attempts++;
            }
            _entityStorage.Insert(stowaway, locker);
        }
    }
}
