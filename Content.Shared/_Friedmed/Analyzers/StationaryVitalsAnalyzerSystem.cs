using Content.Shared._Friedmed.UserInterface;
using Content.Shared._Friedmed.Wounds;

namespace Content.Shared._Friedmed.Analyzers;

public sealed partial class StationaryVitalsAnalyzerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationaryVitalsAnalyzerComponent, AfterAnalyzerUpdatedEvent>(OnAfterUpdated);
        SubscribeLocalEvent<StationaryVitalsAnalyzerComponent, AnalyzerActualTargetUpdatedEvent>(OnTargetUpdated);
    }

    private void OnAfterUpdated(Entity<StationaryVitalsAnalyzerComponent> ent, ref AfterAnalyzerUpdatedEvent args)
    {
        _userInterface.SetUiState(ent.Owner, ent.Comp.UiKey, new DummyBoundUserInterfaceState());

        if (Comp<AnalyzerComponent>(ent).IsUpdating)
            UpdateAppearance(ent);
        else
            ClearAppearance(ent);
    }

    private void OnTargetUpdated(Entity<StationaryVitalsAnalyzerComponent> ent, ref AnalyzerActualTargetUpdatedEvent args)
    {
        if (Comp<AnalyzerComponent>(ent).ActualTarget is null)
            ClearAppearance(ent);
    }

    private void UpdateAppearance(Entity<StationaryVitalsAnalyzerComponent> ent)
    {
        if (Comp<VitalsAnalyzerComponent>(ent).Data is not { } scanned)
        {
            ClearAppearance(ent);
            return;
        }

        // Brain activity
        if (ent.Comp.BrainActivityThresholds.LowestMatch(scanned.BrainHealth) is { } brainActivity)
            _appearance.SetData(ent, VitalsMonitorVisuals.BrainActivity, brainActivity);

        if (ent.Comp.BrainActivityWarningThresholds.LowestMatch(scanned.BrainHealth) is { } brainActivtyWarning)
            _appearance.SetData(ent, VitalsMonitorVisuals.BrainActivityWarning, brainActivtyWarning);
        else
            _appearance.SetData(ent, VitalsMonitorVisuals.BrainActivityWarning, false);
    }

    private void ClearAppearance(Entity<StationaryVitalsAnalyzerComponent> ent)
    {
        _appearance.SetData(ent, VitalsMonitorVisuals.BrainActivity, VitalsMonitorBrainActivity.Blank);
        _appearance.SetData(ent, VitalsMonitorVisuals.BrainActivityWarning, false);
    }
}
