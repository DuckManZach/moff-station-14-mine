using Content.Server._Moffstation.Objectives.Systems;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Objectives.Components;

/// <summary>
/// This is used for objectives with "fill in the blank" options
/// </summary>
[RegisterComponent, Access(typeof(AdLibObjectiveSystem))]
public sealed partial class AdLibObjectiveComponent : Component
{
    /// <summary>
    /// The key is the entry on the localization strong, the value is the eligible group
    /// </summary>
    [DataField]
    public Dictionary<string, List<string>> FillIns = new();

    /// <summary>
    /// What the blanks will be filled in with before an option is selected
    /// </summary>
    [DataField]
    public string DefaultFillIn = "___";

    /// <summary>
    ///
    /// </summary>
    [ViewVariables]
    public Dictionary<string, List<string>> Options = new();

    // All this need to be loc string
    [DataField(required: true)]
    public LocId ObjectiveText;
    [DataField(required: true)]
    public LocId DescriptionText;
}
