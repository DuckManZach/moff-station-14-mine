using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Storage.Components;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Stowaway;

public sealed class StowawaySystem : EntitySystem
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StowawayComponent, PlayerSpawnCompleteEvent>(OnSpawn);
    }

    private void OnSpawn(Entity<StowawayComponent> ent, ref PlayerSpawnCompleteEvent ev)
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
}
