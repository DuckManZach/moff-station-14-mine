using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Research.Prototypes;

/// <summary>
/// Marks the potential upgrades to machines for easy access
/// </summary>
[Prototype]
public sealed partial class MachineUpgradePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The machines this upgrade is applicable to
    /// </summary>
    //TODO: Use tags for this instead of prototypes
    [DataField]
    public HashSet<EntProtoId> BaseMachines = new();

    /// <summary>
    /// The potential upgrades of the machine
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> UpgradedMachines = new();
}
