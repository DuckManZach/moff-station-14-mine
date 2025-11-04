using Content.Server._CD.Spawners;
using Content.Server._Moffstation.Objectives.Components;
using Content.Server._Moffstation.Objectives.Systems;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Server.Storage.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Preferences;
using Content.Shared.Random.Helpers;
using Content.Shared.Station.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Stowaway;

public sealed class StowawaySystem : StationEventSystem<StowawayRuleComponent>
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AntagPreferenceSelectionSystem _antagPref = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IServerPreferencesManager _pref = default!;

    private int SpawnsLeft;
    private List<Entity<EntityStorageComponent>> Lockers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StowawayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (SpawnsLeft <= 0 ||
            Lockers.Count <= 0)
            return;
        EnsureComp<StowawayComponent>(ev.Mob);
        SpawnsLeft--;
    }

    private void OnInit(Entity<StowawayComponent> ent, ref ComponentInit args)
    {
        var pref = (HumanoidCharacterProfile) _pref.GetPreferences(userId).SelectedCharacter;
        if (!TryComp<AntagPreferenceSelectionComponent>(ent.Owner, out var antagPref))
        {
            Log.Warning($"Unable to find AntagPreferenceSelection component for {}!");
            ForceEndSelf(uid, gameRule);
            return;
        }

        var stowaways = new HashSet<EntityUid>();
        for (var i = 0; i < component.Players; i++)
        {
            stowaways.Add(_antagPref.SelectTarget((uid, antagPref), _mind.GetAliveHumans()));
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

    protected override void Started(EntityUid uid, StowawayRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)  // Moffstation - Changed to use the upstream component
    {
        base.Started(uid, component, gameRule, args);

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

        var query = EntityQueryEnumerator<EntityStorageComponent, TransformComponent>();
        while (query.MoveNext(out var locker, out var storage, out var xform))
        {
            if (_stationSystem.GetOwningStation(uid, xform) != station)
                continue;

            if (!_entityStorage.CanInsert(locker, uid, storage))
                continue;

            Lockers.Add((uid, storage));
        }

        SpawnsLeft += component.Players;
    }
}
