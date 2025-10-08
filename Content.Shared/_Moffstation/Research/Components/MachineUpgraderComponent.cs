using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Research.Components;

[RegisterComponent]
public sealed partial class MachineUpgraderComponent : Component
{
    [DataField]
    public TimeSpan BaseUpgradeTime;

    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, float> BaseUpgradeCost = new();

    [DataField]
    public HashSet<EntProtoId> AvailableUpgrades = new();

    [DataField]
    public EntityUid? CurrentTarget;
}

[Serializable, NetSerializable]
public sealed class RPEDConstructionMessage(EntProtoId protoId) : BoundUserInterfaceMessage
{
    public EntProtoId ProtoId = protoId;
}

[Serializable, NetSerializable]
public enum RpedUiKey : byte
{
    Key
}
