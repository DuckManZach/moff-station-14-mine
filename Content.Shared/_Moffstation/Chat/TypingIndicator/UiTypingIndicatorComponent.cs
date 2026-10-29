using Content.Shared.Chat.TypingIndicator;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Chat.TypingIndicator;

/// Shows a typing indicator over anyone who has one of this entity's UIs open.
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedTypingIndicatorSystem))]
public sealed partial class UiTypingIndicatorComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TypingIndicatorPrototype> Indicator;
}
