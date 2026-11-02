namespace Content.Shared._Friedmed.StatusEffects;

[RegisterComponent]
[Access(typeof(GunAccuracyStatusEffectSystem))]
public sealed partial class GunAccuracyStatusEffectComponent : Component
{
    [DataField(required: true)]
    public double AngleRangeModifier;
}
