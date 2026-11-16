using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Stats;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class BodyStatMultiplierComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<BodyStatPrototype>, float> Stats;
}
