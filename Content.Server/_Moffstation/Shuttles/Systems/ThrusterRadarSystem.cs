using Content.Server.Shuttles.Components;
using Content.Shared._Moffstation.Shuttles.Components;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.Shuttles.Systems;

/// <summary>
/// Mirrors each shuttle's powered linear thrusters into <see cref="ShuttleThrusterRadarComponent"/>.
/// </summary>
public sealed partial class ThrusterRadarSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<ShuttleThrusterRadarComponent> _radarQuery = default!;
    [Dependency] private EntityQuery<ThrusterComponent> _thrusterQuery = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    private readonly List<ThrusterRadarEntry> _entries = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        foreach (var ent in EntityQueryEnumerator<ShuttleComponent>())
        {
            var uid = ent.Owner;
            _radarQuery.TryComp(uid, out var radar);

            if (radar != null && radar.NextUpdate > curTime)
                continue;

            _entries.Clear();

            foreach (var thrusters in ent.Comp.LinearThrusters)
            {
                foreach (var thruster in thrusters)
                {
                    var xform = Transform(thruster);
                    var firing = _thrusterQuery.TryComp(thruster, out var thrusterComp) && thrusterComp.Firing;
                    _entries.Add(new ThrusterRadarEntry(xform.LocalPosition, xform.LocalRotation, firing));
                }
            }

            if (_entries.Count == 0)
            {
                if (radar != null)
                    RemComp(uid, radar);

                continue;
            }

            radar ??= AddComp<ShuttleThrusterRadarComponent>(uid);
            radar.NextUpdate = curTime + UpdateInterval;

            if (EntriesMatch(radar.Thrusters))
                continue;

            radar.Thrusters = new List<ThrusterRadarEntry>(_entries);
            Dirty(uid, radar);
        }
    }

    private bool EntriesMatch(List<ThrusterRadarEntry> existing)
    {
        if (existing.Count != _entries.Count)
            return false;

        for (var i = 0; i < existing.Count; i++)
        {
            if (existing[i] != _entries[i])
                return false;
        }

        return true;
    }
}
