using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.BloodBrothers.Components;

/// <summary>
/// This is used for tracking who is eligible to be converted to a blood brother based on their character preference
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodBrotherConvertableComponent : Component
{
    /// <summary>
    /// Whether the entity is valid to be converted to a blood brother
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Valid;

    /// <summary>
    /// Caches preference so that we dont have to use the database, and can short circuit the update function
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool? Preference = null;

    [DataField]
    public ProtoId<FactionIconPrototype> BloodBrotherConvertableIcon = "BloodBrotherConvertable";
}
