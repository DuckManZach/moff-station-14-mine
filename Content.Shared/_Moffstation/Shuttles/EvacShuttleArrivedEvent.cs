using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Shuttles;

/// <summary>
/// Fires when the emergency shuttle docks with the station, but before the round actually ends
/// </summary>
[Serializable, NetSerializable]
public sealed class EvacShuttleArrivedEvent : EntityEventArgs;
