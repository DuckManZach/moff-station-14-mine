using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Friedmed.Stats;

/// Holds a body's stat effectiveness, recomputed from its organs whenever they change.
[RegisterComponent, NetworkedComponent]
[Access(typeof(BodyStatSystem))]
public sealed partial class BodyStatsComponent : Component
{
    // Not a DataField: a cache rebuilt from the organs on startup.
    [ViewVariables]
    public Dictionary<ProtoId<BodyStatPrototype>, float> Stats = new();
}

[Serializable, NetSerializable]
public sealed class BodyStatsComponentState(Dictionary<ProtoId<BodyStatPrototype>, float> stats) : ComponentState
{
    public readonly Dictionary<ProtoId<BodyStatPrototype>, float> Stats = stats;
}

[ByRefEvent]
public readonly record struct BodyStatsChangedEvent(IReadOnlyDictionary<ProtoId<BodyStatPrototype>, float> Stats);
