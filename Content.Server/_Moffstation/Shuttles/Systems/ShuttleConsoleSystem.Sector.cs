using Content.Server._Moffstation.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._Moffstation.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private HyperspaceRiftSystem _hyperspaceRift = default!;

    [SubscribeLocalEvent]
    private void OnSectorFTLMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleFTLSectorMessage args)
    {
        var mapUid = GetEntity(args.Map);

        if (!TryComp<MapComponent>(mapUid, out var map))
            return;

        var consoleUid = GetDroneConsole(ent.Owner);

        if (consoleUid == null)
            return;

        var shuttleUid = Transform(consoleUid.Value).GridUid;

        if (!TryComp(shuttleUid, out ShuttleComponent? shuttleComp) || !shuttleComp.Enabled)
            return;

        // FTL only goes between maps.
        if (Transform(shuttleUid.Value).MapUid == mapUid)
            return;

        if (!_shuttle.CanFTL(shuttleUid.Value, out _) || !_shuttle.CanFTLTo(shuttleUid.Value, map.MapId, ent))
            return;

        var angle = _transform.GetWorldRotation(shuttleUid.Value);

        if (!_hyperspaceRift.TryGetArrival(shuttleUid.Value, mapUid, angle, out var coordinates))
            return;

        var tagEv = new FTLTagEvent();
        RaiseLocalEvent(shuttleUid.Value, ref tagEv);

        var ev = new ShuttleConsoleFTLTravelStartEvent(ent.Owner);
        RaiseLocalEvent(ref ev);

        _shuttle.FTLToCoordinates(shuttleUid.Value, shuttleComp, coordinates, angle);
    }
}
