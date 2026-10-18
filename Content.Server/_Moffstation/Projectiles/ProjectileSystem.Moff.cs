using Content.Shared._Moffstation.Weapons.Ranged.Prediction;
using Robust.Shared.Player;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem
{
    [Dependency] private EntityQuery<PredictedProjectileServerComponent> _predictedQuery;

    /// <summary>
    /// The player whose client is showing its own copy of this projectile, and its hits.
    /// </summary>
    private EntityUid? GetPredictingShooter(EntityUid projectile)
    {
        return _predictedQuery.CompOrNull(projectile)?.Shooter;
    }

    private static Filter ExcludePredicting(Filter filter, EntityUid? predictingShooter)
    {
        return predictingShooter is { } shooter ? filter.RemovePlayerByAttachedEntity(shooter) : filter;
    }

    /// <summary>
    /// Shows the projectile to its shooter again, once it carries on past what their client predicted.
    /// </summary>
    private void StopPredicting(EntityUid projectile)
    {
        if (_predictedQuery.HasComp(projectile))
            RemCompDeferred<PredictedProjectileServerComponent>(projectile);
    }
}
