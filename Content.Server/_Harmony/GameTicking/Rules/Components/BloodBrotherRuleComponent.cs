using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Harmony.GameTicking.Rules.Components;

/// <summary>
/// Game rule for blood brothers. Handles conversion.
/// </summary>
[RegisterComponent, Access(typeof(BloodBrotherRuleSystem)), AutoGenerateComponentPause]
public sealed partial class BloodBrotherRuleComponent : Component
{
    /// <summary>
    /// The antag preference required for someone to be converted into a blood brother.
    /// If null, the check will be skipped.
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype>? RequiredAntagPreference = "BloodBrotherConvertible";

    [DataField]
    public TimeSpan ConvertibleUpdateInterval = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextConvertibleUpdate;
}
