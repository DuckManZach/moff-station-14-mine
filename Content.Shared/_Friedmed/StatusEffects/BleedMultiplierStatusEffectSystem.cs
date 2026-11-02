using Content.Shared._Friedmed.Wounds;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Friedmed.StatusEffects;

public sealed class BleedMultiplierStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BleedMultiplierStatusEffectComponent, StatusEffectRelayedEvent<ModifyBleedLevelEvent>>(OnGetBleedMultiplier);
    }

    private void OnGetBleedMultiplier(Entity<BleedMultiplierStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ModifyBleedLevelEvent> args)
    {
        args.Args = args.Args with { BleedLevel = args.Args.BleedLevel * ent.Comp.Multiplier };
    }
}
