using Content.Shared._Friedmed.Organs;

namespace Content.Shared._Friedmed.Wounds;

public sealed partial class BrainDamageThresholdsSystem : EntitySystem
{
    public void OnAfterBrainDamageChanged(Entity<BrainDamageThresholdsComponent?> ent, Entity<DamageableOrganComponent> brain)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.DisplayDamage = brain.Comp.Damage;
        ent.Comp.DisplayMaxDamage = brain.Comp.MaxDamage;
        Dirty(ent);
    }
}
