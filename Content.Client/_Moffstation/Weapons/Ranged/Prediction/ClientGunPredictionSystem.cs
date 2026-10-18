using Content.Client.Projectiles;
using Content.Shared._Moffstation.Weapons.Ranged.Prediction;
using Content.Shared.Audio;
using Content.Shared.Projectiles;
using Robust.Client.GameObjects;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._Moffstation.Weapons.Ranged.Prediction;

public sealed partial class ClientGunPredictionSystem : GunPredictionSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private Robust.Client.Physics.PhysicsSystem _physics = default!;
    [Dependency] private ProjectileSystem _projectile = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAmbientSoundSystem _ambience = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private EntityQuery<AmbientSoundComponent> _ambienceQuery;
    [Dependency] private EntityQuery<HiddenServerProjectileComponent> _hiddenQuery;
    [Dependency] private EntityQuery<ProjectileComponent> _projectileQuery;
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery;

    /// <summary>
    /// How long a shot waits for its server projectile before it's forgotten.
    /// </summary>
    private static readonly TimeSpan PendingShotLifetime = TimeSpan.FromSeconds(2);

    public override bool ShouldPredictShot(EntityUid? user)
    {
        return Enabled && _timing.IsFirstTimePredicted && user != null && user == _player.LocalEntity;
    }

    public override void MarkFired(EntityUid projectile, EntityUid gun, EntityUid? user, int index)
    {
        var xform = Transform(projectile);
        var predicted = AddComp<PredictedProjectileClientComponent>(projectile);
        predicted.Gun = gun;
        predicted.ShotTick = _timing.CurTick;
        predicted.Index = index;
        predicted.Coordinates = xform.Coordinates;
        predicted.Rotation = xform.LocalRotation;
        _physics.UpdateIsPredicted(projectile);

        // Skip the lerp from the spawn rotation to the firing angle; the first physics step starts a fresh one.
        xform.ActivelyLerping = false;

        var shots = EnsureComp<PredictedShotsComponent>(gun);
        var lifetime = (uint) (PendingShotLifetime.TotalSeconds * _timing.TickRate);
        if (_timing.CurTick.Value > lifetime)
            ForgetShotsBefore(shots, new GameTick(_timing.CurTick.Value - lifetime));

        shots.Shots.Add((_timing.CurTick, index));
    }

    [SubscribeLocalEvent]
    private void OnUpdateIsPredicted(Entity<PredictedProjectileClientComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }

    [SubscribeLocalEvent]
    private void OnStartCollide(Entity<PredictedProjectileClientComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture || !args.OtherFixture.Hard)
            return;

        TryPredictHit(ent, args.OtherEntity);
    }

    // The transform system writes its frame lerp into the real position and rotation, and nothing resets a client-side
    // entity afterwards, so each physics step starts from the pose physics last gave the projectile.
    [SubscribeLocalEvent]
    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        foreach (var ent in EntityQueryEnumerator<PredictedProjectileClientComponent, TransformComponent>())
        {
            RestorePose(ent);
        }
    }

    // TODO gun prediction remove the restore once repredicted ticks stop advancing client-side entities again
    [SubscribeLocalEvent]
    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent ev)
    {
        foreach (var ent in EntityQueryEnumerator<PredictedProjectileClientComponent, TransformComponent>())
        {
            if (!_timing.IsFirstTimePredicted)
            {
                RestorePose(ent);
                continue;
            }

            ent.Comp1.Coordinates = ent.Comp2.Coordinates;
            ent.Comp1.Rotation = ent.Comp2.LocalRotation;
        }
    }

    [SubscribeLocalEvent]
    private void OnServerProjectileStartup(Entity<PredictedProjectileServerComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Shooter != _player.LocalEntity || !TryComp<PredictedShotsComponent>(ent.Comp.Gun, out var shots))
            return;

        // The server fires in the order the client did, so earlier shots that are still pending never got a match.
        ForgetShotsBefore(shots, ent.Comp.ShotTick);
        if (shots.Shots.Remove((ent.Comp.ShotTick, ent.Comp.Index)))
            Hide(ent);
    }

    // Runs while server state is applied, not during prediction, so the hiding isn't rolled back with predicted state.
    [SubscribeLocalEvent]
    private void OnServerProjectileShutdown(Entity<PredictedProjectileServerComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent) || !_hiddenQuery.TryComp(ent, out var hidden))
            return;

        if (hidden.Sprite && _spriteQuery.TryComp(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), true);

        if (hidden.Light)
            _lights.SetEnabled(ent, true);

        if (hidden.Ambience)
            _ambience.SetAmbience(ent, true);

        RemComp<HiddenServerProjectileComponent>(ent);
    }

    private void Hide(EntityUid uid)
    {
        var hidden = EnsureComp<HiddenServerProjectileComponent>(uid);

        if (_spriteQuery.TryComp(uid, out var sprite) && sprite.Visible)
        {
            hidden.Sprite = true;
            _sprite.SetVisible((uid, sprite), false);
        }

        if (_lights.TryGetLight(uid, out var light) && light.Enabled)
        {
            hidden.Light = true;
            _lights.SetEnabled(uid, false, light);
        }

        if (_ambienceQuery.TryComp(uid, out var ambience) && ambience.Enabled)
        {
            hidden.Ambience = true;
            _ambience.SetAmbience(uid, false, ambience);
        }
    }

    private void RestorePose(Entity<PredictedProjectileClientComponent, TransformComponent> ent)
    {
        if (ent.Comp2.ParentUid != ent.Comp1.Coordinates.EntityId)
        {
            _transform.SetCoordinates(ent, ent.Comp2, ent.Comp1.Coordinates, ent.Comp1.Rotation);
            return;
        }

        _transform.SetLocalPositionNoLerp(ent, ent.Comp1.Coordinates.Position, ent.Comp2);
        _transform.SetLocalRotationNoLerp(ent, ent.Comp1.Rotation, ent.Comp2);
    }

    private static void ForgetShotsBefore(PredictedShotsComponent shots, GameTick tick)
    {
        for (var i = shots.Shots.Count - 1; i >= 0; i--)
        {
            if (shots.Shots[i].Tick < tick)
                shots.Shots.RemoveAt(i);
        }
    }

    private bool TryPredictHit(Entity<PredictedProjectileClientComponent> ent, EntityUid target)
    {
        if (!_timing.IsFirstTimePredicted ||
            _player.LocalEntity is not { } shooter ||
            !_projectileQuery.TryComp(ent, out var projectile) ||
            projectile.ProjectileSpent ||
            projectile is { Weapon: null, OnlyCollideWhenShot: true })
        {
            return false;
        }

        RaiseNetworkEvent(new PredictedProjectileHitEvent(
            GetNetEntity(ent.Comp.Gun),
            ent.Comp.ShotTick,
            ent.Comp.Index,
            GetNetEntity(target),
            _transform.GetMapCoordinates(target)));

        _projectile.PredictCollide((ent, projectile), target, shooter);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        // TODO gun prediction remove this once the client reliably detects collisions
        foreach (var ent in EntityQueryEnumerator<PredictedProjectileClientComponent>())
        {
            var contacts = _physics.GetContacts(ent.Owner);
            while (contacts.MoveNext(out var contact))
            {
                var ours = contact.EntityA == ent.Owner;
                var ourFixtureId = ours ? contact.FixtureAId : contact.FixtureBId;
                var otherFixture = ours ? contact.FixtureB : contact.FixtureA;

                if (!contact.Enabled ||
                    ourFixtureId != SharedProjectileSystem.ProjectileFixture ||
                    otherFixture is not { Hard: true })
                {
                    continue;
                }

                if (TryPredictHit(ent, ours ? contact.EntityB : contact.EntityA))
                    break;
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Hide server projectiles once they reach the target the shooter hit.
        foreach (var ent in EntityQueryEnumerator<PredictedProjectileHitComponent, SpriteComponent, TransformComponent>())
        {
            if (!ent.Comp2.Visible)
                continue;

            if (!ent.Comp1.Origin.TryDistance(EntityManager, _transform, ent.Comp3.Coordinates, out var distance) ||
                distance >= ent.Comp1.Distance)
            {
                _sprite.SetVisible((ent.Owner, ent.Comp2), false);
            }
        }
    }
}
