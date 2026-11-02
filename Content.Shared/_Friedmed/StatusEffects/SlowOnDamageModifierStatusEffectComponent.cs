namespace Content.Shared._Friedmed.StatusEffects;

[RegisterComponent]
[Access(typeof(SlowOnDamageModifierStatusEffectSystem))]
public sealed partial class SlowOnDamageModifierStatusEffectComponent : Component
{
    [DataField(required: true)]
    public float Modifier;
}
