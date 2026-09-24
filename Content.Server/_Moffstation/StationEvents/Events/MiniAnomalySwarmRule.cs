using Content.Server._Moffstation.StationEvents.Components;
using Content.Server.Anomaly;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.StationEvents.Events;

public sealed partial class MiniAnomalySwarmRule : StationEventSystem<MiniAnomalySwarmRuleComponent>
{
    [Dependency] private AnomalySystem _anomaly = default!;

    protected override void Started(EntityUid uid, MiniAnomalySwarmRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (comp.Entries.Count == 0)
            return;

        if (!TryGetRandomStation(out var chosenStation))
            return;

        if (!TryComp<StationDataComponent>(chosenStation, out var stationData))
            return;

        var grid = StationSystem.GetLargestGrid((chosenStation.Value, stationData));
        if (grid == null)
            return;

        var sharedEntry = RobustRandom.Pick(comp.Entries);
        var amount = comp.Count.Next(RobustRandom);
        for (var i = 0; i < amount; i++)
        {
            var entry = comp.SameType ? sharedEntry : RobustRandom.Pick(comp.Entries);
            var proto = entry.Infection is { } infection && RobustRandom.Prob(comp.InfectionChance)
                ? infection
                : entry.Anomaly;

            _anomaly.SpawnOnRandomGridLocation(grid.Value, proto);
        }
    }
}
