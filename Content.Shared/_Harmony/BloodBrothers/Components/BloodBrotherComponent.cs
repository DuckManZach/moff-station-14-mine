using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Harmony.BloodBrothers.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BloodBrotherComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Brotherhood;

    [DataField, AutoNetworkedField]
    public float ExtraConversionChance = 0.1f;

    [DataField]
    public ProtoId<FactionIconPrototype> BloodBrotherIcon = "BloodBrotherFaction";

    public override bool SessionSpecific => true;
}
