using Content.Server._Moffstation.Shuttles.Components;
using Content.Server._Moffstation.Spawners;
using Content.Server.Communications;
using Content.Server.GameTicking.Events;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Moffstation.CCVar;
using Content.Shared.CCVar;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.Shuttles.Systems;

/// Starts the round with the crew aboard the evac shuttle as it FTLs to the station
public sealed partial class EvacArrivalSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ArrivalsSystem _arrivals = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private StationSystem _station = default!;

    [Dependency] private EntityQuery<EvacArrivalComponent> _evacArrivalsQuery;
    [Dependency] private EntityQuery<ShuttleComponent> _shuttleQuery;

    private static readonly ProtoId<TagPrototype> DockTag = "DockEmergency";
    private static readonly LocId CallBlockedReason = "evac-arrival-call-blocked";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var ent in EntityQueryEnumerator<EvacArrivalComponent, ShuttleComponent>())
        {
            if (ent.Comp1.DepartTime is { } depart && depart <= _timing.CurTime)
                Depart((ent, ent.Comp1, ent.Comp2));
        }
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
                !_shuttleQuery.TryComp(shuttle, out var shuttleComp) ||
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
    private void OnCallShuttleAttempt(ref CommunicationConsoleCallShuttleAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        foreach (var _ in EntityQueryEnumerator<EvacArrivalComponent>())
        {
            args.Cancelled = true;
            args.Reason = Loc.GetString(CallBlockedReason);
            return;
        }
    }

    [SubscribeLocalEvent]
    private void OnGetArrivalsSpawnGrid(Entity<StationEmergencyShuttleComponent> ent, ref GetArrivalsSpawnGridEvent args)
    {
        if (ent.Comp.EmergencyShuttle is { } shuttle
            && _evacArrivalsQuery.TryComp(shuttle, out var arrival)
            && arrival.State != EvacArrivalState.Returning)
            args.Grid = shuttle;
    }

    [SubscribeLocalEvent]
    private void OnEvacDepartureCheck(Entity<EvacArrivalComponent> ent, ref EmergencyShuttleEvacDepartureCheckEvent args)
    {
        args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnFTLStarted(Entity<EvacArrivalComponent> ent, ref FTLStartedEvent args)
    {
        if (ent.Comp.State == EvacArrivalState.Returning && args.FromMapUid != null)
            _arrivals.DumpChildren(ent, ref args);
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
                    ClearArrivalStatus((ent, ent.Comp, shuttle));
                break;
        }
    }

    // Sends the shuttle back to the abyss, or leaves it docked if evac has been called meanwhile.
    private void Depart(Entity<EvacArrivalComponent, ShuttleComponent> ent)
    {
        // If it still has a cooldown for some reason, block it for now
        if (HasComp<FTLComponent>(ent))
            return;

        ent.Comp1.State = EvacArrivalState.Returning;
        ent.Comp1.DepartTime = null;
        _shuttle.FTLToCoordinates(ent, ent.Comp2, ent.Comp1.Origin, ent.Comp1.OriginRotation);
    }

    private void ClearArrivalStatus(Entity<EvacArrivalComponent, ShuttleComponent> ent)
    {
        ent.Comp2.FTLCooldownOverride = ent.Comp1.CooldownOverride;
        RemCompDeferred<EvacArrivalComponent>(ent);
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
