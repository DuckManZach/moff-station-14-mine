using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Traits.Components;

[RegisterComponent]
public sealed partial class AddictedTraitComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Substance = "Nicotine";

    [DataField]
    public TimeSpan WithdrawalInterval = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(2);

    public float Timer;
}
