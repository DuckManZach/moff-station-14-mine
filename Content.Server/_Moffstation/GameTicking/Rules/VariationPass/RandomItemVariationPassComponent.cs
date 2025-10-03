using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.GameTicking.Rules.VariationPass;

[RegisterComponent]
public sealed partial class RandomItemVariationPassComponent : Component
{
    /// <summary>
    /// How much more likely are people in certain departments to start with the item?
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DepartmentPrototype>, float>? Weight;
}
