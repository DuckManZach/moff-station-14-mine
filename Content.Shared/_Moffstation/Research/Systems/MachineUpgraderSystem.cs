using System.Linq;
using Content.Shared._Moffstation.Research.Components;
using Content.Shared._Moffstation.Research.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Research.Systems;

public sealed class MachineUpgraderSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<MachineUpgraderComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MachineUpgraderComponent, RPEDConstructionMessage>(OnConstructionAttempt);
    }

    private void OnAfterInteract(Entity<MachineUpgraderComponent> ent, ref AfterInteractEvent args)
    {
        ent.Comp.AvailableUpgrades.Clear();

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
        if (avaliableUpgrades.Count == 0)
            _popup.PopupClient("no upgrades available", args.User);
        else
        {
            _popup.PopupClient("Upgrades found!", args.User);
            ent.Comp.AvailableUpgrades = avaliableUpgrades;
            ent.Comp.CurrentTarget = args.Target;
            _ui.TryOpenUi(ent.Owner, RpedUiKey.Key, args.User);
        }
    }

    private void OnConstructionAttempt(Entity<MachineUpgraderComponent> ent, ref RPEDConstructionMessage args)
    {
        // Exit if the RPED doesn't actually know the supplied prototype
        if (!ent.Comp.AvailableUpgrades.Contains(args.ProtoId))
            return;

        if (!_protoManager.HasIndex(args.ProtoId))
            return;

        if (ent.Comp.CurrentTarget is not { } target)
            return;

        var gridUid = _transform.GetGrid(target);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        var tile = _mapSystem.GetTileRef(gridUid.Value, mapGrid, Transform(target).Coordinates);
        var position = _mapSystem.TileIndicesFor(gridUid.Value, mapGrid, Transform(target).Coordinates);

        var doAfterArgs = new DoAfterArgs(EntityManager, ent, 0, ev, uid, target: args.Target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = false,
            BlockDuplicate = false
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            QueueDel(effect);
    }
}
