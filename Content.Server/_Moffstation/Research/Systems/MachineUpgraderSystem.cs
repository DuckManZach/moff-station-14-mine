using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Moffstation.Research.Components;
using Content.Shared._Moffstation.Research.Prototypes;
using Content.Shared.Construction.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Systems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Research.Systems;

public sealed partial class MachineUpgraderSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedResearchSystem _research = default!;

    private static readonly EntProtoId UpgradeEffect = "EffectRCDConstruct2";
    private const float MaterialScanRadius = 2f;

    public override void Initialize()
    {
        SubscribeLocalEvent<MachineUpgraderComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MachineUpgraderComponent, RPEDConstructionMessage>(OnConstructionAttempt);
        SubscribeLocalEvent<MachineUpgraderComponent, RPEDDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<MachineUpgraderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (Comp<MetaDataComponent>(target).EntityPrototype is not { } proto)
            return;

        var techDbUid = FindAnyTechDatabaseUid();
        var availableUpgrades = new HashSet<EntProtoId>();

        foreach (var upgrade in _proto.EnumeratePrototypes<MachineUpgradePrototype>())
        {
            if (!upgrade.BaseMachines.Contains(proto.ID))
                continue;

            if (upgrade.RequiredTechnology is { } tech &&
                (techDbUid == null || !_research.IsTechnologyUnlocked(techDbUid.Value, tech.Id)))
                continue;

            availableUpgrades.UnionWith(upgrade.UpgradedMachines);
        }

        if (availableUpgrades.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("rped-no-upgrades"), target, args.User);
            return;
        }

        ent.Comp.AvailableUpgrades = availableUpgrades;
        ent.Comp.CurrentTarget = target;
        Dirty(ent);

        args.Handled = true;
        _ui.TryOpenUi(ent.Owner, RpedUiKey.Key, args.User);
    }

    private void OnConstructionAttempt(Entity<MachineUpgraderComponent> ent, ref RPEDConstructionMessage args)
    {
        if (!ent.Comp.AvailableUpgrades.Contains(args.ProtoId))
            return;

        if (!_proto.HasIndex(args.ProtoId))
            return;

        if (ent.Comp.CurrentTarget is not { } target)
            return;

        if (Comp<MetaDataComponent>(target).EntityPrototype is not { } originalProto)
            return;

        var cost = ComputeUpgradeCost(ent.Comp, args.ProtoId, originalProto.ID);

        if (!HasSufficientMaterials(args.Actor, ent.Owner, target, cost))
        {
            _popup.PopupEntity(Loc.GetString("rped-insufficient-materials"), ent.Owner, args.Actor);
            return;
        }

        var effect = Spawn(UpgradeEffect, Transform(target).Coordinates);

        var ev = new RPEDDoAfterEvent(args.ProtoId, GetNetEntity(effect), cost);
        var doAfterArgs = new DoAfterArgs(EntityManager, args.Actor, ent.Comp.UpgradeTime, ev, ent.Owner, target: target, used: ent.Owner)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = true,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            QueueDel(effect);
    }

    private void OnDoAfter(Entity<MachineUpgraderComponent> ent, ref RPEDDoAfterEvent args)
    {
        if (args.Effect is { } effectNet)
            QueueDel(GetEntity(effectNet));

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.CurrentTarget is not { } target || TerminatingOrDeleted(target))
            return;

        var user = args.User;

        if (!TryConsumeFromWorld(user, ent.Owner, target, args.Cost))
        {
            _popup.PopupEntity(Loc.GetString("rped-insufficient-materials"), ent.Owner, user);
            return;
        }

        var targetXform = Transform(target);
        var pos = targetXform.Coordinates;
        var rot = targetXform.LocalRotation;

        QueueDel(target);

        var upgraded = Spawn(args.Upgrade, pos);
        Transform(upgraded).LocalRotation = rot;

        _audio.PlayPvs(ent.Comp.UpgradeSound, upgraded);

        ent.Comp.CurrentTarget = null;
        ent.Comp.AvailableUpgrades.Clear();
        Dirty(ent);
    }

    private bool HasSufficientMaterials(EntityUid user, EntityUid rped, EntityUid target, Dictionary<string, int> cost)
    {
        if (cost.Count == 0 || cost.Values.All(v => v <= 0))
            return true;

        var available = TallyAvailable(GatherMaterialEntities(user, rped, target));
        foreach (var (mat, needed) in cost)
        {
            if (needed <= 0)
                continue;
            if (!available.TryGetValue(mat, out var have) || have < needed)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks availability then consumes the minimum items needed from hands and floor.
    /// </summary>
    private bool TryConsumeFromWorld(EntityUid user, EntityUid rped, EntityUid target, Dictionary<string, int> cost)
    {
        if (cost.Count == 0 || cost.Values.All(v => v <= 0))
            return true;

        var entities = GatherMaterialEntities(user, rped, target);
        var available = TallyAvailable(entities);

        foreach (var (mat, needed) in cost)
        {
            if (needed <= 0)
                continue;
            if (!available.TryGetValue(mat, out var have) || have < needed)
                return false;
        }

        // Greedily consume, using the minimum items required from each entity.
        var remaining = new Dictionary<string, int>(cost);
        foreach (var entity in entities)
        {
            if (remaining.Values.All(v => v <= 0))
                break;

            if (!TryComp<PhysicalCompositionComponent>(entity, out var physComp))
                continue;

            TryComp<StackComponent>(entity, out var entityStack);
            var stackCount = entityStack?.Count ?? 1;

            // How many items of this entity are needed to cover remaining costs?
            var itemsNeeded = 0;
            foreach (var (mat, perItem) in physComp.MaterialComposition)
            {
                if (perItem <= 0 || !remaining.TryGetValue(mat, out var need) || need <= 0)
                    continue;
                itemsNeeded = Math.Max(itemsNeeded, (int)Math.Ceiling((double)need / perItem));
            }

            if (itemsNeeded <= 0)
                continue;

            var itemsToConsume = Math.Min(itemsNeeded, stackCount);

            foreach (var (mat, perItem) in physComp.MaterialComposition)
            {
                if (remaining.ContainsKey(mat))
                    remaining[mat] -= perItem * itemsToConsume;
            }

            if (entityStack != null)
                _stack.TryUse((entity, entityStack), itemsToConsume);
            else
                QueueDel(entity);
        }

        return true;
    }

    private Dictionary<string, int> TallyAvailable(List<EntityUid> entities)
    {
        var available = new Dictionary<string, int>();
        foreach (var entity in entities)
        {
            if (!TryComp<PhysicalCompositionComponent>(entity, out var physComp))
                continue;
            var count = TryComp<StackComponent>(entity, out var stack) ? stack.Count : 1;
            foreach (var (mat, perItem) in physComp.MaterialComposition)
            {
                available.TryAdd(mat, 0);
                available[mat] += perItem * count;
            }
        }
        return available;
    }

    private List<EntityUid> GatherMaterialEntities(EntityUid user, EntityUid rped, EntityUid target)
    {
        var entities = new List<EntityUid>();

        // Held items and anything stored inside them.
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held == rped)
                continue;

            if (TryComp<StorageComponent>(held, out var heldStorage))
            {
                foreach (var stored in heldStorage.Container.ContainedEntities)
                {
                    entities.Add(stored);
                }
            }

            entities.Add(held);
        }

        // Equipped inventory slots and anything stored inside them.
        if (_inventory.TryGetContainerSlotEnumerator(user, out var slotEnum))
        {
            while (slotEnum.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is not { } worn)
                    continue;

                if (TryComp<StorageComponent>(worn, out var wornStorage))
                {
                    foreach (var stored in wornStorage.Container.ContainedEntities)
                    {
                        entities.Add(stored);
                    }
                }

                entities.Add(worn);
            }
        }

        // Nearby world entities (same approach as construction system).
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(
            user,
            MaterialScanRadius,
            nearby,
            LookupFlags.Contained | LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate);
        foreach (var entity in nearby)
        {
            if (entity == rped || entity == target || entity == user)
                continue;
            if (_interaction.InRangeUnobstructed(user, entity, MaterialScanRadius) &&
                _container.IsInSameOrParentContainer(user, entity))
            {
                entities.Add(entity);
            }
        }

        return entities;
    }

    private Dictionary<string, int> ComputeUpgradeCost(MachineUpgraderComponent comp, EntProtoId upgradeTo, EntProtoId upgradeFrom)
    {
        var upgradeCost = GetMachineCost(upgradeTo, comp);
        var originalCost = GetMachineCost(upgradeFrom, comp);

        var finalCost = new Dictionary<string, int>(upgradeCost);
        foreach (var (mat, amount) in originalCost)
        {
            if (!finalCost.ContainsKey(mat))
                continue;
            finalCost[mat] -= amount / 2;
            if (finalCost[mat] <= 0)
                finalCost.Remove(mat);
        }

        return finalCost;
    }

    private Dictionary<string, int> GetMachineCost(EntProtoId entityProto, MachineUpgraderComponent comp)
    {
        var cost = new Dictionary<string, int>();

        foreach (var (mat, amount) in comp.BaseMachineCost)
        {
            cost[mat.Id] = amount;
        }

        if (!TryGetBoardComponent(entityProto, out var board))
            return cost;

        foreach (var (mat, amount) in GetBoardMaterialCost(board))
        {
            cost.TryAdd(mat, 0);
            cost[mat] += amount * comp.CostMultiplier;
        }

        return cost;
    }

    private Dictionary<string, int> GetBoardMaterialCost(MachineBoardComponent board)
    {
        var materials = new Dictionary<string, int>();

        foreach (var (stackId, amount) in board.StackRequirements)
        {
            if (!_proto.TryIndex(stackId, out var stackProto))
                continue;

            if (!_proto.TryIndex<EntityPrototype>(stackProto.Spawn, out var spawnProto))
                continue;

            if (!spawnProto.TryGetComponent<PhysicalCompositionComponent>(out var physComp, EntityManager.ComponentFactory))
                continue;

            foreach (var (mat, matAmount) in physComp.MaterialComposition)
            {
                materials.TryAdd(mat, 0);
                materials[mat] += matAmount * amount;
            }
        }

        var genericParts = board.ComponentRequirements.Values.Concat(board.TagRequirements.Values);
        foreach (var info in genericParts)
        {
            if (!_proto.TryIndex<EntityPrototype>(info.DefaultPrototype, out var defaultProto))
                continue;

            if (!defaultProto.TryGetComponent<PhysicalCompositionComponent>(out var physComp, EntityManager.ComponentFactory))
                continue;

            foreach (var (mat, matAmount) in physComp.MaterialComposition)
            {
                materials.TryAdd(mat, 0);
                materials[mat] += matAmount * info.Amount;
            }
        }

        return materials;
    }

    private bool TryGetBoardComponent(EntProtoId entityProtoId, [NotNullWhen(true)] out MachineBoardComponent? board)
    {
        board = null;
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryGetComponent<MachineBoardComponent>(out var comp, EntityManager.ComponentFactory))
                continue;
            if (comp.Prototype != entityProtoId)
                continue;
            board = comp;
            return true;
        }
        return false;
    }

    private EntityUid? FindAnyTechDatabaseUid()
    {
        var query = EntityQueryEnumerator<TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            return uid;
        }
        return null;
    }
}
