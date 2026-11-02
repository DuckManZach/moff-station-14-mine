using Content.Shared._Friedmed.Wounds;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Friedmed.StatusEffects;

public sealed class PainkillerStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PainkillerStatusEffectComponent, StatusEffectRelayedEvent<GetPainEvent>>(OnGetPain);
    }

    private void OnGetPain(Entity<PainkillerStatusEffectComponent> ent, ref StatusEffectRelayedEvent<GetPainEvent> args)
    {
        args.Args = args.Args with { Pain = args.Args.Pain - ent.Comp.Effectiveness };
    }
}
