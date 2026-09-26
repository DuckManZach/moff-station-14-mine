using Content.Server._Moffstation.Shuttles.Systems;

namespace Content.Server._Moffstation.Shuttles.Components;

/// Batteries with this component are topped up when evac shuttle (rename pending) docks with the station.
[RegisterComponent, Access(typeof(EvacArrivalSystem))]
public sealed partial class BatteryRefillOnArrivalComponent : Component;
