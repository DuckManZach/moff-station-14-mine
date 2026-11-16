using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Stats;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class BodyStatContributorComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<BodyStatPrototype>, float> Stats;
}

/// Raised on a body to collect its stats; relayed to its organs.
[ByRefEvent]
public readonly record struct GetBodyStatsEvent(Dictionary<ProtoId<BodyStatPrototype>, float> Stats);
