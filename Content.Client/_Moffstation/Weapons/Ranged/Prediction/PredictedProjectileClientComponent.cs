using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Moffstation.Weapons.Ranged.Prediction;

/// <summary>
/// The local player's own copy of a projectile they fired.
/// </summary>
[RegisterComponent, Access(typeof(ClientGunPredictionSystem))]
public sealed partial class PredictedProjectileClientComponent : Component
{
    [DataField]
    public EntityUid Gun;

    // Not a DataField: GameTick has no serializer.
    [ViewVariables]
    public GameTick ShotTick;

    [DataField]
    public int Index;

    /// <summary>
    /// Where physics last put the projectile, before the transform system's frame lerp moved it.
    /// </summary>
    [DataField]
    public EntityCoordinates Coordinates;

    /// <summary>
    /// The local rotation physics last gave the projectile, before the transform system's frame lerp turned it.
    /// </summary>
    [DataField]
    public Angle Rotation;
}

/// <summary>
/// Shots the local player fired from this gun whose server projectile hasn't arrived yet.
/// </summary>
[RegisterComponent, Access(typeof(ClientGunPredictionSystem))]
public sealed partial class PredictedShotsComponent : Component
{
    // Not a DataField: GameTick has no serializer, and this is only client bookkeeping.
    [ViewVariables]
    public readonly List<(GameTick Tick, int Index)> Shots = new();
}
