using Content.Shared._Friedmed.Wounds;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Friedmed.StatusEffects;

public sealed class CardiacOutputModifierStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardiacOutputModifierStatusEffectComponent, StatusEffectRelayedEvent<ModifiedCardiacOutputEvent>>(OnModifiedCardiacOutput);
    }

    private void OnModifiedCardiacOutput(Entity<CardiacOutputModifierStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ModifiedCardiacOutputEvent> args)
    {
        args.Args = args.Args with { Output = MathF.Max(ent.Comp.Output, args.Args.Output) };
    }
}
