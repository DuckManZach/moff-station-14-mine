using System.ComponentModel.DataAnnotations;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Objectives.Components;

/// <summary>
/// This is used to mark objectives which select random people and prioritize high value targets
/// </summary>
[RegisterComponent]
public sealed partial class AntagPreferenceSelectionComponent : Component
{
    /// <summary>
    /// The antag prototype selected from each possible target's preferences
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype> AntagTargetPrototype = "HighValueTarget";

    /// <summary>
    /// If the selected individual doesn't have the preference, what are the odds they will be selected anyway?
    /// </summary>
    [DataField]
    public float UnselectedChance = 0.3f;
}
