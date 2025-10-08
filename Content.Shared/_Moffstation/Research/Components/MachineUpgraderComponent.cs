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
}

[Serializable, NetSerializable]
public sealed class RPEDSystemMessage(EntProtoId protoId) : BoundUserInterfaceMessage
{
    public EntProtoId ProtoId = protoId;
}

[Serializable, NetSerializable]
public sealed class RPEDUpdateUpgrades(HashSet<EntProtoId> protos) : BoundUserInterfaceMessage
{
    public HashSet<EntProtoId> Protos = protos;
}
