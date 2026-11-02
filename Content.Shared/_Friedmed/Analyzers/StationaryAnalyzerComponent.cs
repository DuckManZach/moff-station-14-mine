using Robust.Shared.GameStates;

namespace Content.Shared._Friedmed.Analyzers;

[RegisterComponent, NetworkedComponent]
[Access(typeof(StationaryAnalyzerSystem))]
public sealed partial class StationaryAnalyzerComponent : Component
{
    [DataField]
    public LocId ScanningPatient = "vitals-monitor-scanning-patient";

    [DataField]
    public LocId ScanningStrap = "vitals-monitor-scanning-strap";
}
