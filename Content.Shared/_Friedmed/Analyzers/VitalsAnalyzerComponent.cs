using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Friedmed.Analyzers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(VitalsAnalyzerSystem))]
public sealed partial class VitalsAnalyzerComponent : Component
{
    [DataField, AutoNetworkedField]
    public VitalsData? Data;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class VitalsData
{
    [DataField]
    public float BrainHealth;

    [DataField]
    public (int, int) BloodPressure;

    [DataField]
    public int HeartRate;

    [DataField]
    public float HeartStrain;

    [DataField]
    public int Etco2;

    [DataField]
    public int RespiratoryRate;

    [DataField]
    public float RespiratoryRateModifier;

    [DataField]
    public float Spo2;

    [DataField]
    public LocId Etco2Name;

    [DataField]
    public LocId Etco2GasName;

    [DataField]
    public LocId Spo2Name;

    [DataField]
    public LocId Spo2GasName;

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, (FixedPoint2 InBloodstream, FixedPoint2 Metabolites)>? Reagents;

    [DataField]
    public bool NonMedicalReagents;

    [DataField]
    public float BloodLevel;

    /// Shock as a fraction of the highest pain threshold.
    [DataField]
    public float Pain;

    [DataField]
    public EntProtoId? PainStatus;

    /// Only the parts with something to report, null when sampled without wounds.
    [DataField]
    public List<BodyPartData>? BodyParts;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class BodyPartData
{
    [DataField]
    public EntProtoId Organ;

    [DataField]
    public float Health;

    [DataField]
    public bool Missing;

    [DataField]
    public List<BodyConditionData> Conditions = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class BodyConditionData
{
    [DataField]
    public EntProtoId Wound;

    [DataField]
    public int Count;

    [DataField]
    public FixedPoint2 Damage;

    [DataField]
    public LocId? Description;

    /// Summed over the grouped wounds, on the same scale as the bleed alert's severity.
    [DataField]
    public float BleedLevel;

    [DataField]
    public bool Tended;
}
