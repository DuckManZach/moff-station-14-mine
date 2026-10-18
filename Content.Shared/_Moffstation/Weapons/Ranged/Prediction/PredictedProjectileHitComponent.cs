using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Moffstation.Weapons.Ranged.Prediction;

/// <summary>
/// A server projectile whose hit was reported by the shooter. It keeps flying until it has covered the distance to
/// the target, so other players see it arrive, and is then deleted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(GunPredictionSystem))]
public sealed partial class PredictedProjectileHitComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityCoordinates Origin;

    [DataField, AutoNetworkedField]
    public float Distance;
}
