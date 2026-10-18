using Content.Shared.Damage.Components;
using Content.Shared.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Player;

namespace Content.Client.Projectiles;

public sealed partial class ProjectileSystem
{
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery;

    /// <summary>
    /// Shows a hit by the local player's own copy of a projectile. The server deals the damage once it has checked
    /// the hit.
    /// </summary>
    public void PredictCollide(Entity<ProjectileComponent> projectile, EntityUid target, EntityUid shooter)
    {
        projectile.Comp.ProjectileSpent = true;
        var damage = projectile.Comp.Damage;

        if (!Deleted(target))
        {
            if (_damageableQuery.HasComp(target) && damage.GetTotal() > 0)
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Local());

            _gun.PlayImpactSound(target, damage, projectile.Comp.SoundHit, projectile.Comp.ForceSound, shooter);
        }

        if (projectile.Comp.ImpactEffect != null)
            OnProjectileImpact(new ImpactEffectEvent(projectile.Comp.ImpactEffect, GetNetCoordinates(Transform(projectile).Coordinates)));

        QueueDel(projectile);
    }
}
