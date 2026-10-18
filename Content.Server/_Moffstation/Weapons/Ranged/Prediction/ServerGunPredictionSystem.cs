using Content.Server.Movement.Components;
using Content.Server.Projectiles;
using Content.Shared._Moffstation.CCVar;
using Content.Shared._Moffstation.Weapons.Ranged.Prediction;
using Content.Shared.Projectiles;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.Weapons.Ranged.Prediction;

public sealed partial class ServerGunPredictionSystem : GunPredictionSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ProjectileSystem _projectile = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery;
    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery;
    [Dependency] private EntityQuery<LagCompensationComponent> _lagCompensationQuery;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery;
    [Dependency] private EntityQuery<ProjectileComponent> _projectileQuery;

    private float _coordinateDeviation;
    private float _lowestCoordinateDeviation;
    private float _aabbEnlargement;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, MoffCCVars.GunPredictionCoordinateDeviation, v => _coordinateDeviation = v, true);
        Subs.CVar(_cfg, MoffCCVars.GunPredictionLowestCoordinateDeviation, v => _lowestCoordinateDeviation = v, true);
        Subs.CVar(_cfg, MoffCCVars.GunPredictionAabbEnlargement, v => _aabbEnlargement = v, true);
    }

    public override bool ShouldPredictShot(EntityUid? user)
    {
        return false;
    }

    public override void MarkFired(EntityUid projectile, EntityUid gun, EntityUid? user, int index)
    {
        if (!Enabled || user == null || !_actorQuery.HasComp(user))
            return;

        var predicted = EnsureComp<PredictedProjectileServerComponent>(projectile);
        predicted.Gun = gun;
        predicted.ShotTick = _timing.CurTick;
        predicted.Index = index;
        predicted.Shooter = user.Value;
        Dirty(projectile, predicted);
    }

    // Client messages are handled on the tick the client sent them, which is the tick its projectile hit on.
    [SubscribeNetworkEvent]
    private void OnPredictedProjectileHit(PredictedProjectileHitEvent ev, EntitySessionEventArgs args)
    {
        if (!Enabled ||
            args.SenderSession.AttachedEntity is not { } shooter ||
            !TryGetEntity(ev.Gun, out var gun) ||
            !TryGetEntity(ev.Target, out var target) ||
            !TryFindProjectile(gun.Value, ev.ShotTick, ev.Index, shooter, out var predicted))
        {
            return;
        }

        predicted.Comp.Hit = true;

        if (!_projectileQuery.TryComp(predicted, out var projectile) ||
            !_physicsQuery.TryComp(predicted, out var physics) ||
            projectile.ProjectileSpent ||
            !Collides((predicted, physics), target.Value, ev.TargetCoordinates, args.SenderSession))
        {
            return;
        }

        _projectile.ProjectileCollide((predicted, projectile, physics), target.Value, clientReported: true);

        if (!projectile.DeleteOnCollide || !projectile.ProjectileSpent)
            return;

        var hit = EnsureComp<PredictedProjectileHitComponent>(predicted);
        hit.Origin = _transform.GetMoverCoordinates(predicted);
        hit.Distance = hit.Origin.TryDistance(EntityManager, _transform, _transform.GetMoverCoordinates(target.Value), out var distance)
            ? distance
            : 0f;
        Dirty(predicted, hit);
    }

    private bool TryFindProjectile(
        EntityUid gun,
        GameTick shotTick,
        int index,
        EntityUid shooter,
        out Entity<PredictedProjectileServerComponent> projectile)
    {
        foreach (var predicted in EntityQueryEnumerator<PredictedProjectileServerComponent>())
        {
            if (predicted.Comp.Gun != gun || predicted.Comp.ShotTick != shotTick || predicted.Comp.Index != index)
                continue;

            projectile = predicted;
            return predicted.Comp.Shooter == shooter && !predicted.Comp.Hit;
        }

        projectile = default;
        return false;
    }

    /// <summary>
    /// Whether the projectile is touching the target where the shooter saw it, allowing for the shooter's ping.
    /// </summary>
    private bool Collides(
        Entity<PhysicsComponent> projectile,
        EntityUid target,
        MapCoordinates clientCoordinates,
        ICommonSession session)
    {
        if (!_fixturesQuery.TryComp(target, out var fixtures))
            return false;

        var projectileCoordinates = _transform.GetMapCoordinates(projectile);
        var targetCoordinates = _transform.GetMapCoordinates(target);
        if (projectileCoordinates.MapId != targetCoordinates.MapId)
            return false;

        if (_lagCompensationQuery.TryComp(target, out var lagCompensation))
        {
            // Use 1.5 due to the trip buffer.
            var ping = TimeSpan.FromMilliseconds(session.Ping);
            var sentTime = _timing.CurTime - ping * 1.5;
            MapCoordinates? lowest = null;
            EntityCoordinates? sent = null;

            foreach (var (time, coordinates, _) in lagCompensation.Positions)
            {
                sent = coordinates;
                if (time >= sentTime)
                    break;

                if (lowest == null && time >= sentTime - ping)
                    lowest = _transform.ToMapCoordinates(coordinates);
            }

            if (sent != null)
                targetCoordinates = _transform.ToMapCoordinates(sent.Value);

            if (clientCoordinates.InRange(targetCoordinates, _coordinateDeviation) ||
                lowest != null && clientCoordinates.InRange(lowest.Value, _lowestCoordinateDeviation))
            {
                targetCoordinates = clientCoordinates;
            }
        }
        else if (clientCoordinates.InRange(targetCoordinates, _coordinateDeviation))
        {
            targetCoordinates = clientCoordinates;
        }

        var transform = new Transform(targetCoordinates.Position, 0);
        var bounds = new Box2(transform.Position, transform.Position);

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if ((fixture.CollisionLayer & projectile.Comp.CollisionMask) == 0)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                bounds = bounds.Union(fixture.Shape.ComputeAABB(transform, i));
            }
        }

        bounds = bounds.Enlarged(_aabbEnlargement);
        if (bounds.Contains(projectileCoordinates.Position))
            return true;

        // The projectile may have just passed through the target within this tick.
        var velocity = _physics.GetLinearVelocity(projectile, projectile.Comp.LocalCenter, projectile.Comp);
        return bounds.Contains(projectileCoordinates.Position + velocity / _timing.TickRate / 1.5f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var ent in EntityQueryEnumerator<PredictedProjectileHitComponent, TransformComponent>())
        {
            if (!ent.Comp1.Origin.TryDistance(EntityManager, _transform, ent.Comp2.Coordinates, out var distance) ||
                distance >= ent.Comp1.Distance)
            {
                QueueDel(ent);
            }
        }
    }
}
