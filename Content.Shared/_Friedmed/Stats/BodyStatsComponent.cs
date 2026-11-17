using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Stats;

/// Holds a body's stat effectiveness, recomputed from its organs whenever they change.
[RegisterComponent]
[Access(typeof(BodyStatSystem))]
public sealed partial class BodyStatsComponent : Component
{
    // Not a DataField: a cache rebuilt from the organs on startup.
    [ViewVariables]
    public Dictionary<ProtoId<BodyStatPrototype>, float> Stats = new();
}

[ByRefEvent]
public readonly record struct BodyStatsChangedEvent(IReadOnlyDictionary<ProtoId<BodyStatPrototype>, float> Stats);
