using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Content.Client.Popups;
using Content.Client.UserInterface.Controls;
using Content.Shared._Moffstation.Research.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Moffstation.RPED;

public sealed class RPEDMenuBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private SimpleRadialMenu? _menu;

    public RPEDMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<MachineUpgraderComponent>(Owner, out var rped))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(ConvertToButtons(rped));

        _menu.OpenOverMouseScreenPosition();

        _menu.OnClose += OnClose;
    }

    private void OnClose()
    {
        if (!EntMan.TryGetComponent<MachineUpgraderComponent>(Owner, out var rped))
            return;

        rped.CurrentTarget = null;
        rped.AvailableUpgrades.Clear();
    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(MachineUpgraderComponent rped)
    {
        ValueList<RadialMenuActionOptionBase> options = new();
        foreach (var protoId in rped.AvailableUpgrades)
        {
            var prototype = _prototypeManager.Index(protoId);

            var option = new RadialMenuActionOption<EntProtoId>(HandleMenuOptionClick, prototype)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(prototype),
                ToolTip = GetTooltip(prototype, rped)
            };
            options.Add(option);
        }

        return options;
    }

    private string GetTooltip(EntProtoId proto, MachineUpgraderComponent rped)
    {
        string name;
        if (_prototypeManager.TryIndex(proto, out var entProto))
            name = Loc.GetString(entProto.Name);
        else
            name = Loc.GetString(proto.Id);

        name = OopsConcat(char.ToUpper(name[0]).ToString(), name.Remove(0, 1));

        EntProtoId? originalProtoId = null;
        if (rped.CurrentTarget is { } target &&
            EntMan.TryGetComponent<MetaDataComponent>(target, out var meta) &&
            meta.EntityPrototype is { } originalProto)
        {
            originalProtoId = new EntProtoId(originalProto.ID);
        }

        var cost = ComputeUpgradeCost(rped, proto, originalProtoId ?? proto);

        if (cost.Count == 0)
            return name;

        var sb = new StringBuilder(name);
        sb.Append("\nCost: ");

        var first = true;
        foreach (var (matId, amount) in cost.OrderBy(kv => kv.Key))
        {
            if (!first)
                sb.Append(", ");
            first = false;

            var matName = _prototypeManager.TryIndex<MaterialPrototype>(matId, out var matProto)
                ? Loc.GetString(matProto.Name)
                : matId;

            // MaterialComposition stores 100 units per sheet/bar.
            var displayAmount = (int)Math.Ceiling(amount / 100.0);
            sb.Append(matName);
            sb.Append(" ×");
            sb.Append(displayAmount);
        }

        return sb.ToString();
    }

    private static string OopsConcat(string a, string b)
    {
        // This exists to prevent Roslyn being clever and compiling something that fails sandbox checks.
        return a + b;
    }

    private void HandleMenuOptionClick(EntProtoId proto)
    {
        // A predicted message cannot be used here as the RCD UI is closed immediately
        // after this message is sent, which will stop the server from receiving it
        SendMessage(new RPEDConstructionMessage(proto));
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
            cost[mat] += amount;
        }

        return cost;
    }

    private Dictionary<string, int> GetBoardMaterialCost(MachineBoardComponent board)
    {
        var materials = new Dictionary<string, int>();

        foreach (var (stackId, amount) in board.StackRequirements)
        {
            if (!_prototypeManager.TryIndex(stackId, out var stackProto))
                continue;

            if (!_prototypeManager.TryIndex<EntityPrototype>(stackProto.Spawn, out var spawnProto))
                continue;

            if (!spawnProto.TryGetComponent<PhysicalCompositionComponent>(out var physComp, EntMan.ComponentFactory))
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
            if (!_prototypeManager.TryIndex<EntityPrototype>(info.DefaultPrototype, out var defaultProto))
                continue;

            if (!defaultProto.TryGetComponent<PhysicalCompositionComponent>(out var physComp, EntMan.ComponentFactory))
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
        foreach (var proto in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryGetComponent<MachineBoardComponent>(out var comp, EntMan.ComponentFactory))
                continue;
            if (comp.Prototype != entityProtoId)
                continue;
            board = comp;
            return true;
        }
        return false;
    }
}
