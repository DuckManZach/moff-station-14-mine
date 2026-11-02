namespace Content.Shared._Friedmed.StatusEffects;

[RegisterComponent]
[Access(typeof(BleedMultiplierStatusEffectSystem))]
public sealed partial class BleedMultiplierStatusEffectComponent : Component
{
    [DataField(required: true)]
    public float Multiplier;
}
