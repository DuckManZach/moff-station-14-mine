using Content.Server._Moffstation.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    /// When the Evac shuttle is FTLing, this returns true when it's the evac departure that ends the round, false if it's something else.
    private bool IsEvacDeparture(EntityUid shuttle)
    {
        var ev = new EmergencyShuttleEvacDepartureCheckEvent();
        RaiseLocalEvent(shuttle, ref ev);
        return !ev.Cancelled;
    }
}
