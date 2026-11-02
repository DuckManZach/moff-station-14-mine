using Content.Shared._Friedmed.Wounds;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Friedmed.StatusEffects;

public sealed class RespiratoryRateModifierStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RespiratoryRateModifierStatusEffectComponent, StatusEffectRelayedEvent<ModifiedRespiratoryRateEvent>>(OnModifiedRespiratoryRate);
    }

    private void OnModifiedRespiratoryRate(Entity<RespiratoryRateModifierStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ModifiedRespiratoryRateEvent> args)
    {
        args.Args = args.Args with { Rate = MathF.Max(ent.Comp.Rate, args.Args.Rate) };
    }
}
