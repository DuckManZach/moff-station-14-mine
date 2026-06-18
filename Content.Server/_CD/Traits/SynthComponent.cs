using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._CD.Traits;

/// <summary>
/// Set players' blood to coolant, and is used to notify them of ion storms
/// </summary>
[RegisterComponent, Access(typeof(SynthSystem))]
public sealed partial class SynthComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> BloodReagentPrototype = "SynthBlood";
}
