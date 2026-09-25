using System.Numerics;
using Content.Server._Moffstation.Shuttles.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Events;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Moffstation.CCVar;
using Content.Shared.CCVar;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.Shuttles.Systems;

/// Starts the round with the crew aboard the evac shuttle as it FTLs to the station
public sealed partial class EvacArrivalSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActorSystem _actor = default!;
    [Dependency] private EmergencyShuttleSystem _emergency = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private StationSystem _station = default!;

    [Dependency] private EntityQuery<ArrivalsBlacklistComponent> _blacklistQuery;
    [Dependency] private EntityQuery<MobStateComponent> _mobQuery;

    private static readonly ProtoId<TagPrototype> DockTag = "DockEmergency";
    private static readonly LocId DumpedMessage = "evac-arrival-dumped-from-shuttle";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        foreach (var ent in EntityQueryEnumerator<EvacArrivalComponent, ShuttleComponent>())
        {
            if (ent.Comp1.State != EvacArrivalState.Docked || ent.Comp1.DepartTime is not { } depart || depart > curTime)
                continue;

            if (_emergency.EmergencyShuttleArrived)
            {
                EndArrival((ent, ent.Comp1, ent.Comp2));
                continue;
            }

            // Still in FTL cooldown from arriving
            if (HasComp<FTLComponent>(ent))
                continue;

            ent.Comp1.State = EvacArrivalState.Returning;
            ent.Comp1.DepartTime = null;
            _shuttle.FTLToCoordinates(ent, ent.Comp2, ent.Comp1.Origin, ent.Comp1.OriginRotation);
        }
    }

    /// True while the evac shuttle is bringing the crew in or docked at the station.
    public bool IsArrivalPhase(Entity<EvacArrivalComponent?> shuttle)
    {
        return Resolve(shuttle, ref shuttle.Comp, false) && shuttle.Comp.State != EvacArrivalState.Returning;
    }

    // Maps are loaded before this and players are spawned right after, in the same tick.
    [SubscribeLocalEvent]
    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (!_cfg.GetCVar(MoffCCVars.StartAtArrivals) || !_cfg.GetCVar(CCVars.EmergencyShuttleEnabled))
            return;

        foreach (var station in EntityQueryEnumerator<StationEmergencyShuttleComponent>())
        {
            if (station.Comp.EmergencyShuttle is not { } shuttle ||
                !TryComp<ShuttleComponent>(shuttle, out var shuttleComp) ||
                _station.GetLargestGrid(station.Owner) is not { } target)
                continue;

            var xform = Transform(shuttle);
            var arrival = EnsureComp<EvacArrivalComponent>(shuttle);
            arrival.Station = station;
            arrival.Origin = xform.Coordinates;
            arrival.OriginRotation = xform.LocalRotation;
            // No cooldown, so an early evac call can still move the shuttle
            arrival.CooldownOverride = shuttleComp.FTLCooldownOverride;
            shuttleComp.FTLCooldownOverride = TimeSpan.Zero;

            _shuttle.FTLToDock(shuttle,
                shuttleComp,
                target,
                startupTime: 0f,
                hyperspaceTime: _cfg.GetCVar(MoffCCVars.EvacArrivalFTLTime),
                priorityTag: DockTag);
        }
    }

    [SubscribeLocalEvent]
    private void OnFTLStarted(Entity<EvacArrivalComponent> ent, ref FTLStartedEvent args)
    {
        if (ent.Comp.State != EvacArrivalState.Returning || args.FromMapUid is not { } fromMap)
            return;

        var toDump = new List<Entity<TransformComponent>>();
        FindDumpChildren(ent, toDump);
        foreach (var (uid, xform) in toDump)
        {
            var rotation = xform.LocalRotation;
            _transform.SetCoordinates(uid, new EntityCoordinates(fromMap, Vector2.Transform(xform.LocalPosition, args.FTLFrom)));
            _transform.SetWorldRotation(uid, args.FromRotation + rotation);

            if (_actor.TryGetSession(uid, out var session) && session != null)
                _chat.DispatchServerMessage(session, Loc.GetString(DumpedMessage));
        }
    }

    [SubscribeLocalEvent]
    private void OnFTLCompleted(Entity<EvacArrivalComponent> ent, ref FTLCompletedEvent args)
    {
        switch (ent.Comp.State)
        {
            case EvacArrivalState.InTransit:
                ent.Comp.State = EvacArrivalState.Docked;
                ent.Comp.DepartTime = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(MoffCCVars.EvacArrivalDockTime));
                RefillStationBatteries(ent.Comp.Station);
                break;
            case EvacArrivalState.Returning:
                if (TryComp<ShuttleComponent>(ent, out var shuttle))
                    EndArrival((ent, ent.Comp, shuttle));
                // Evac was called while it was flying back
                if (_emergency.EmergencyShuttleArrived)
                    _emergency.DockSingleEmergencyShuttle(ent.Comp.Station);
                break;
        }
    }

    private void EndArrival(Entity<EvacArrivalComponent, ShuttleComponent> ent)
    {
        ent.Comp2.FTLCooldownOverride = ent.Comp1.CooldownOverride;
        RemCompDeferred<EvacArrivalComponent>(ent);
    }

    private void FindDumpChildren(EntityUid uid, List<Entity<TransformComponent>> toDump)
    {
        var xform = Transform(uid);

        if (_mobQuery.HasComp(uid) || _blacklistQuery.HasComp(uid))
        {
            toDump.Add((uid, xform));
            return;
        }

        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            FindDumpChildren(child, toDump);
        }
    }

    // Engineering has to wait for the shuttle before they can get the power running.
    private void RefillStationBatteries(EntityUid station)
    {
        foreach (var battery in EntityQueryEnumerator<BatteryComponent>())
        {
            if (_station.GetOwningStation(battery) == station)
                _battery.SetCharge(battery.AsNullable(), battery.Comp.MaxCharge);
        }
    }
}
