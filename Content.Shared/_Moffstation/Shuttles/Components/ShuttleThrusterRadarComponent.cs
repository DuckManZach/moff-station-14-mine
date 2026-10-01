using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Moffstation.Shuttles.Components;

/// <summary>
/// The powered thrusters on a grid, for drawing on shuttle radars at any range.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ShuttleThrusterRadarComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public List<ThrusterRadarEntry> Thrusters = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}

[Serializable, NetSerializable]
public readonly record struct ThrusterRadarEntry(Vector2 LocalPosition, Angle LocalRotation, bool Firing);
