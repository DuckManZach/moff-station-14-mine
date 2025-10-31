using Content.Server._Moffstation.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Stowaway;

public sealed class StowawaySystem : GameRuleSystem<StowawayComponent>
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StowawayComponent, ComponentInit>(OnInit);
    }

    protected override void Started(EntityUid uid, StowawayComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var validLockers = new List<(EntityUid, EntityStorageComponent)>();

        var query = EntityQueryEnumerator<EntityStorageComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var storage, out var xform))
        {
            if (_stationSystem.GetOwningStation(uid, xform) != ev.Station)
                continue;

            if (!_entityStorage.CanInsert(ent.Owner, uid, storage))
                continue;

            validLockers.Add((uid, storage));
        }

        var (locker, storageComp) = _random.Pick(validLockers);
        _entityStorage.Insert(ent.Owner, locker, storageComp);
    }

    private void OnInit()
}
