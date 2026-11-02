using Content.Shared.FixedPoint;

namespace Content.Shared._Friedmed.StatusEffects;

[RegisterComponent]
[Access(typeof(PainkillerStatusEffectSystem))]
public sealed partial class PainkillerStatusEffectComponent : Component
{
    [DataField(required: true)]
    public FixedPoint2 Effectiveness;
}
