using System.Linq;
using Content.Shared._Moffstation.Research.Components;
using Content.Shared._Moffstation.Research.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Research.Systems;

public sealed class MachineUpgraderSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MachineUpgraderComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<MachineUpgraderComponent> ent, ref AfterInteractEvent args)
    {
        var avaliableUpgrades = new HashSet<EntProtoId>();
        var allUpgrades = _protoManager.EnumeratePrototypes<MachineUpgradePrototype>();

        // See if theres a valid target
        if (args.Target is not { } target)
            return;

        // Attempt to get entity prototype
        if (Comp<MetaDataComponent>(target).EntityPrototype is not { } proto)
            return;

        foreach (var upgrade in allUpgrades)
        {
            if (upgrade.BaseMachines.Contains(proto))
            {
                avaliableUpgrades.UnionWith(upgrade.UpgradedMachines);
            }
        }

        _popup.PopupClient(
            avaliableUpgrades.Count == 0 ? "No upgrades available" : "Upgrades found!",
            args.User);

    }
}
