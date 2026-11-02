using Content.Server.Zombies;
using Content.Shared._Friedmed.EntityEffects;
using Content.Shared.EntityEffects;

namespace Content.Server._Friedmed.EntityEffects;

public sealed partial class ZombifySystem : EntityEffectSystem<MetaDataComponent, Zombify>
{
    [Dependency] private ZombieSystem _zombie = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<Zombify> args)
    {
        _zombie.ZombifyEntity(ent);
    }
}
