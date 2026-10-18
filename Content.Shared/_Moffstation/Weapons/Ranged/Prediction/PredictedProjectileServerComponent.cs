using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Moffstation.Weapons.Ranged.Prediction;

/// <summary>
/// A server projectile that the shooter's client also spawned. Removed once the projectile survives a hit, so the
/// shooter sees it again.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(GunPredictionSystem))]
public sealed partial class PredictedProjectileServerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Gun;

    // Not a DataField: GameTick has no serializer, and the tick means nothing once saved.
    [ViewVariables, AutoNetworkedField]
    public GameTick ShotTick;

    [DataField, AutoNetworkedField]
    public int Index;

    [DataField, AutoNetworkedField]
    public EntityUid Shooter;

    /// <summary>
    /// Whether the shooter already reported a hit for this projectile.
    /// </summary>
    [DataField]
    public bool Hit;
}

/// <summary>
/// Sent by the shooter's client when its own copy of a projectile hits something.
/// </summary>
[Serializable, NetSerializable]
public sealed class PredictedProjectileHitEvent(
    NetEntity gun,
    GameTick shotTick,
    int index,
    NetEntity target,
    MapCoordinates targetCoordinates) : EntityEventArgs
{
    public readonly NetEntity Gun = gun;
    public readonly GameTick ShotTick = shotTick;
    public readonly int Index = index;
    public readonly NetEntity Target = target;
    public readonly MapCoordinates TargetCoordinates = targetCoordinates;
}
