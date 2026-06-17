using Content.Shared.DoAfter;
using Content.Shared.Materials;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Research.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MachineUpgraderComponent : Component
{
    [DataField]
    public TimeSpan UpgradeTime = TimeSpan.FromSeconds(15);

    [DataField]
    public SoundSpecifier UpgradeSound = new SoundPathSpecifier("/Audio/Items/rped.ogg");

    /// <summary>
    /// Base material cost added on top of the machine board cost, mirroring the flatpacker's base cost.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> BaseMachineCost = new();

    [DataField]
    public int CostMultiplier = 2;

    /// <summary>
    /// Upgrade options for the currently targeted machine, sent to client so the radial menu can display them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntProtoId> AvailableUpgrades = new();

    [DataField, AutoNetworkedField]
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

[Serializable, NetSerializable]
public sealed partial class RPEDDoAfterEvent : DoAfterEvent
{
    [DataField]
    public EntProtoId Upgrade { get; private set; }

    [DataField("fx")]
    public NetEntity? Effect { get; private set; }

    /// <summary>
    /// Material cost keyed by material prototype ID, in storage volume units.
    /// </summary>
    [DataField]
    public Dictionary<string, int> Cost { get; private set; } = new();

    private RPEDDoAfterEvent() { }

    public RPEDDoAfterEvent(EntProtoId upgrade, NetEntity? effect, Dictionary<string, int> cost)
    {
        Upgrade = upgrade;
        Effect = effect;
        Cost = cost;
    }

    public override DoAfterEvent Clone() => this;
}
