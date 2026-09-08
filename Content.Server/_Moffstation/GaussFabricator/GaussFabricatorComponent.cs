using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Moffstation.GaussFabricator;

[RegisterComponent]
[Access(typeof(GaussFabricatorSystem))]
public sealed partial class GaussFabricatorComponent : Component
{
    /// <summary>
    /// Fraction of received electrical power (W) added as heat (J/s) to the
    /// surrounding atmosphere. 0.05 = 5% of draw becomes waste heat.
    /// </summary>
    [DataField]
    public float HeatMultiplier = 0.05f;

    /// <summary>
    /// Minimum draw rate players can configure in watts
    /// </summary>
    [DataField]
    public float MinDrawRate;

    /// <summary>
    /// Maximum draw rate players can configure in watts
    /// </summary>
    [DataField]
    public float MaxDrawRate = 250000f;

    /// <summary>
    /// Ambient temperature outside which the reading counts as bad, in kelvin.
    /// </summary>
    [DataField]
    public MinMax TemperatureAcceptable = new(20, 200);

    /// <summary>
    /// Ambient temperature inside which the reading counts as optimal, in kelvin.
    /// </summary>
    [DataField]
    public MinMax TemperatureOptimal = new(60, 130);

    /// <summary>
    /// Ambient pressure outside which the reading counts as bad, in kPa.
    /// </summary>
    [DataField]
    public MinMax PressureAcceptable = new(20, 300);

    /// <summary>
    /// Ambient pressure inside which the reading counts as optimal, in kPa.
    /// </summary>
    [DataField]
    public MinMax PressureOptimal = new(80, 120);

    /// <summary>
    /// Output speed multiplier for a reading outside its acceptable range.
    /// </summary>
    [DataField]
    public float BadMultiplier = 0.2f;

    /// <summary>
    /// Output speed multiplier for a reading that is acceptable but not optimal.
    /// </summary>
    [DataField]
    public float NormalMultiplier = 1f;

    /// <summary>
    /// Output speed multiplier for a reading inside its optimal range.
    /// </summary>
    [DataField]
    public float OptimalMultiplier = 1.5f;
}
