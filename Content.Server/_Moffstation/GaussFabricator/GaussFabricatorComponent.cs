using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.GaussFabricator;

[RegisterComponent]
[Access(typeof(GaussFabricatorSystem))]
public sealed partial class GaussFabricatorComponent : Component
{
    /// <summary>Entity prototype to spawn when the battery reaches full charge.</summary>
    [DataField]
    public EntProtoId OutputPrototype = "MoffRefinedConductorIngot";

    /// <summary>
    /// Fraction of received electrical power (W) added as heat (J/s) to the
    /// surrounding atmosphere. 0.05 = 5% of draw becomes waste heat.
    /// </summary>
    [DataField]
    public float HeatMultiplier = 0.05f;
}