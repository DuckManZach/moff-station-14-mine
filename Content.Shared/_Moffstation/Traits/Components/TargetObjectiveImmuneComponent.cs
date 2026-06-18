namespace Content.Shared._Moffstation.Traits.Components;

/// <summary>
/// Entities with this component cannot be selected as targets for kill or kidnap objectives.
/// </summary>
[RegisterComponent]
public sealed partial class TargetObjectiveImmuneComponent : Component;
