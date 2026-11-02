using Content.Shared._Friedmed.Organs;
using Content.Shared._Friedmed.Wounds;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Friedmed.StatusEffects;

public sealed class OxygenatableOrganHealingStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OxygenatableOrganHealingStatusEffectComponent, StatusEffectRelayedEvent<BeforeHealOrganOxygenDamage>>(OnBeforeHealOrganOxygenDamage);
    }

    private void OnBeforeHealOrganOxygenDamage(Entity<OxygenatableOrganHealingStatusEffectComponent> ent, ref StatusEffectRelayedEvent<BeforeHealOrganOxygenDamage> args)
    {
        args.Args = args.Args with { Heal = true };
    }
}
