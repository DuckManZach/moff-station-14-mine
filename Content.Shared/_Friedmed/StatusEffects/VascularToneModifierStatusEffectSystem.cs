using Content.Shared._Friedmed.Wounds;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Friedmed.StatusEffects;

public sealed class VascularToneModifierStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VascularToneModifierStatusEffectComponent, StatusEffectRelayedEvent<ModifiedVascularToneEvent>>(OnModifiedVascularTone);
    }

    private void OnModifiedVascularTone(Entity<VascularToneModifierStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ModifiedVascularToneEvent> args)
    {
        args.Args = args.Args with { Tone = MathF.Max(ent.Comp.Tone, args.Args.Tone) };
    }
}
