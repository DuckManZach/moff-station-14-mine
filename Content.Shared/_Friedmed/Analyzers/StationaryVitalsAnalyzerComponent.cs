using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Friedmed.Analyzers;

[RegisterComponent, NetworkedComponent]
[Access(typeof(StationaryVitalsAnalyzerSystem))]
public sealed partial class StationaryVitalsAnalyzerComponent : Component
{
    [DataField]
    public SortedDictionary<float, VitalsMonitorBrainActivity> BrainActivityThresholds;

    [DataField]
    public SortedDictionary<float, bool> BrainActivityWarningThresholds;

    [DataField(required: true)]
    public Enum UiKey;
}

[Serializable, NetSerializable]
public enum VitalsMonitorVisuals : byte
{
    BrainActivity,
    BrainActivityWarning,
}

[Serializable, NetSerializable]
public enum VitalsMonitorBrainActivity : byte
{
    Blank,
    Okay,
    Bad,
    VeryBad,
}
