using Robust.Shared.GameStates;

namespace Content.Shared._Friedmed.Examine;

[RegisterComponent, NetworkedComponent]
public sealed partial class ExaminableUserInterfaceComponent : Component
{
    [DataField(required: true)]
    public Enum UiKey;

    [DataField]
    public bool RequiresDetailsRange = true;
}
