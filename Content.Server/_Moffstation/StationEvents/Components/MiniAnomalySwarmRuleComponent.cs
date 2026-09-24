using Content.Server._Moffstation.StationEvents.Events;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.StationEvents.Components;

/// <summary>
/// Spawns a swarm of mini-anomalies and mini-infection traps on the station. See <see cref="MiniAnomalySwarmRule"/>.
/// </summary>
[RegisterComponent, Access(typeof(MiniAnomalySwarmRule))]
public sealed partial class MiniAnomalySwarmRuleComponent : Component
{
    [DataField(required: true)]
    public List<MiniAnomalySwarmEntry> Entries = new();

    [DataField]
    public MinMax Count = new(6, 10);

    /// <summary>
    /// Chance for each spawn to be the entry's infection trap instead of its anomaly.
    /// </summary>
    [DataField]
    public float InfectionChance = 0.2f;

    /// <summary>
    /// If true, one entry is picked for the whole swarm; otherwise each spawn picks its own.
    /// </summary>
    [DataField]
    public bool SameType;
}

[DataDefinition]
public sealed partial class MiniAnomalySwarmEntry
{
    [DataField(required: true)]
    public EntProtoId Anomaly;

    [DataField]
    public EntProtoId? Infection;
}
