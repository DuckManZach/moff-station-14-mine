using Content.Shared.Hands.Components;

namespace Content.Shared._Moffstation.Traits.Components;

[RegisterComponent]
public sealed partial class AmputeeComponent : Component
{
    [DataField(required: true)]
    public HandLocation Location;
}
