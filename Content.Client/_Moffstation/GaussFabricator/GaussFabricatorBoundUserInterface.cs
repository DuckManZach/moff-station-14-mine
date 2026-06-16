using Content.Shared._Moffstation.GaussFabricator;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Moffstation.GaussFabricator;

[UsedImplicitly]
public sealed class GaussFabricatorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private GaussFabricatorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GaussFabricatorWindow>();
        _window.Title = Loc.GetString("gauss-fabricator-window-title");
        _window.OnAdjustDrawRate += delta => SendMessage(new GaussFabricatorAdjustDrawRateMessage(delta));
        _window.OnToggle += on => SendMessage(new GaussFabricatorToggleMessage(on));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is GaussFabricatorBuiState cast)
            _window?.UpdateState(cast);
    }
}