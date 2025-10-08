using Content.Client.Popups;
using Content.Client.UserInterface.Controls;
using Content.Shared._Moffstation.Research.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Moffstation.RPED;

public sealed class RPEDMenuBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private IEnumerable<RadialMenuOptionBase> Options = null!;

    private SimpleRadialMenu? _menu;

    public RPEDMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<MachineUpgraderComponent>(Owner, out var rcd))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(Options);

        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(HashSet<EntProtoId> prototypes)
    {
        ValueList<RadialMenuActionOptionBase> options = new();
        foreach (var protoId in prototypes)
        {
            var prototype = _prototypeManager.Index(protoId);

            var option = new RadialMenuActionOption<EntProtoId>(HandleMenuOptionClick, prototype)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(prototype),
                ToolTip = GetTooltip(prototype)
            };
            options.Add(option);
        }

        return options;
    }

    private string GetTooltip(EntProtoId proto)
    {
        string tooltip;

        if (_prototypeManager.TryIndex(proto, out var entProto)) // don't use Resolve because this can be a tile
        {
            tooltip = Loc.GetString(entProto.Name);
        }
        else
        {
            tooltip = Loc.GetString(proto.Id);
        }

        tooltip = OopsConcat(char.ToUpper(tooltip[0]).ToString(), tooltip.Remove(0, 1));

        return tooltip;
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
        SendMessage(new RPEDSystemMessage(proto));


        if (_playerManager.LocalSession?.AttachedEntity == null)
            return;

        var name = Loc.GetString(proto.Id);

        if (_prototypeManager.TryIndex(proto, out var entProto)) // don't use Resolve because this can be a tile
        {
            name = entProto.Name;
        }

        var msg = Loc.GetString("rcd-component-change-build-mode", ("name", name));


        // Popup message
        var popup = EntMan.System<PopupSystem>();
        popup.PopupClient(msg, Owner, _playerManager.LocalSession.AttachedEntity);
    }
}
