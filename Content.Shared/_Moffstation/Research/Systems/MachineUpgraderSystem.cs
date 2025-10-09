using System.Linq;
using Content.Shared._Moffstation.Research.Components;
using Content.Shared._Moffstation.Research.Prototypes;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Research.Systems;

public sealed class MachineUpgraderSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedFlatpackSystem _flatpack = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MachineUpgraderComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MachineUpgraderComponent, RPEDConstructionMessage>(OnConstructionAttempt);
        SubscribeLocalEvent<MachineUpgraderComponent, DoAfterAttemptEvent<RPEDDoAfterEvent>>(OnDoAfterAttempt);
        SubscribeLocalEvent<MachineUpgraderComponent, RPEDDoAfterEvent>(OnDoAfter);
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

        var ev = new RPEDDoAfterEvent(target, 0);

        var doAfterArgs = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(1), ev, target)
        {
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = false,
            BlockDuplicate = false
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfterAttempt(EntityUid uid, MachineUpgraderComponent component, DoAfterAttemptEvent<RPEDDoAfterEvent> args)
    {
        if (args.Event?.DoAfter?.Args == null)
            return;

        if (args.Event.Target is not { } target)
        {
            args.Cancel();
            return;
        }

        var location = Transform(target).Coordinates;

        var gridUid = _transform.GetGrid(location);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
        {
            args.Cancel();
            return;
        }
    }

    private void OnDoAfter(Entity<MachineUpgraderComponent> ent, ref RPEDDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            // Delete the effect entity if the do-after was cancelled (server-side only)
            if (_net.IsServer)
                QueueDel(GetEntity(args.Effect));
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.CurrentTarget is not { } target)
            return;

        var location = Transform(target).Coordinates;

        var gridUid = _transform.GetGrid(location);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        var tile = _mapSystem.GetTileRef(gridUid.Value, mapGrid, location);
        var position = _mapSystem.TileIndicesFor(gridUid.Value, mapGrid, location);

        // Ensure the RCD operation is still valid
        if (!IsRCDOperationStillValid(uid, component, gridUid.Value, mapGrid, tile, position, args.Target, args.User))
            return;

        // Finalize the operation (this should handle prediction properly)
        FinalizeRCDOperation(uid, component, gridUid.Value, mapGrid, tile, position, args.Direction, args.Target, args.User);

        // Play audio and consume charges
        _audio.PlayPredicted(component.SuccessSound, uid, args.User);
        _sharedCharges.AddCharges(uid, -args.Cost);
    }

    private Dictionary<ProtoId<MaterialPrototype>, float> GetCost(EntProtoId upgrade, EntProtoId original)
    {
        _flatpack.GetFlatpackCreationCost()
    }
}
