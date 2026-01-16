namespace Content.Server._Harmony.GameTicking.Rules.Components;

/// <summary>
/// Game rule for blood brothers. Handles conversion.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(BloodBrotherRuleSystem))]
public sealed partial class BloodBrotherRuleComponent : Component
{
    /// <summary>
    /// How often should convertable icons be updated?
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan IconRefreshRate = TimeSpan.FromSeconds(1);
}
