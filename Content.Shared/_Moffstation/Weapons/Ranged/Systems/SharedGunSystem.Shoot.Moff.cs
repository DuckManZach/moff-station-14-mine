using System.Numerics;
using Content.Shared._Moffstation.Weapons.Ranged.Components;
using Content.Shared._Moffstation.Weapons.Ranged.Prediction;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

// Shooting merged from the server and client GunSystem overrides, so the shooter's client can spawn its own
// projectiles. Anything the client didn't do before stays server-only.
public abstract partial class SharedGunSystem
{
    [Dependency] private GunPredictionSystem _gunPrediction = default!;
    [Dependency] private SharedCameraRecoilSystem _cameraRecoil = default!;

    public void Shoot(
        Entity<GunComponent> gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false)
    {
        userImpulse = true;
        var isServer = _netManager.IsServer;

        if (isServer && user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, gun, ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
            {
                userImpulse = false;
                return;
            }
        }

        var predict = _gunPrediction.ShouldPredictShot(user);

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        var mapAngle = mapDirection.ToAngle();
        var recoil = -mapDirection;
        var angle = GetRecoilAngle(gun, mapAngle);

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromEnt = Maps.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(Maps.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        // Update shot based on the recoil
        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);

        var shotProjectiles = new List<EntityUid>(ammo.Count);
        var index = 0;

        foreach (var (ent, shootable) in ammo)
        {
            // pneumatic cannon doesn't shoot bullets it just throws them, ignore ammo handling
            if (throwItems && ent != null)
            {
                Recoil(user, recoil, gun.Comp.CameraRecoilScalarModified);

                if (isServer)
                    ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, user);
                else if (IsClientSide(ent.Value))
                    Del(ent.Value);
                else
                    RemoveShootable(ent.Value);

                continue;
            }

            // TODO: Clean this up in a gun refactor at some point - too much copy pasting
            switch (shootable)
            {
                // Cartridge shoots something else
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        if (isServer || predict)
                        {
                            var uid = Spawn(cartridge.Prototype, fromEnt);
                            CreateAndFireProjectiles(uid, cartridge);

                            if (isServer)
                            {
                                RaiseLocalEvent(ent!.Value, new AmmoShotEvent()
                                {
                                    FiredProjectiles = shotProjectiles,
                                });
                            }
                        }
                        else
                        {
                            MuzzleFlash(gun, cartridge, mapDirection.ToAngle(), user);
                            Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                        }

                        SetCartridgeSpent(ent!.Value, cartridge, true);
                        Recoil(user, recoil, gun.Comp.CameraRecoilScalarModified);

                        if (isServer && cartridge.DeleteOnSpawn)
                            Del(ent.Value);
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                    }

                    if (isServer)
                    {
                        // Something like ballistic might want to leave it in the container still
                        if (!cartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value))
                            EjectCartridge(ent.Value, angle);

                        Dirty(ent!.Value, cartridge);
                    }
                    else if (IsClientSide(ent!.Value))
                    {
                        Del(ent.Value);
                    }

                    break;
                // Ammo shoots itself
                case AmmoComponent newAmmo:
                    if (ent == null)
                        break;

                    // Networked ammo isn't predicted: the client can't spawn its own copy of an entity the server owns.
                    if (isServer || predict && IsClientSide(ent.Value))
                    {
                        CreateAndFireProjectiles(ent.Value, newAmmo);
                    }
                    else
                    {
                        MuzzleFlash(gun, newAmmo, mapDirection.ToAngle(), user);
                        Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);

                        if (IsClientSide(ent.Value))
                            Del(ent.Value);
                        else
                            RemoveShootable(ent.Value);
                    }

                    Recoil(user, recoil, gun.Comp.CameraRecoilScalarModified);
                    break;
                case HitscanAmmoComponent:
                    if (!isServer)
                    {
                        Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                        Recoil(user, recoil, gun.Comp.CameraRecoilScalarModified);

                        if (ent != null && IsClientSide(ent.Value))
                            Del(ent.Value);

                        break;
                    }

                    if (ent == null)
                        break;

                    var hitscanEv = new HitscanTraceEvent
                    {
                        FromCoordinates = fromCoordinates,
                        ShotDirection = mapDirection.Normalized(),
                        Gun = gun,
                        Shooter = user,
                        Target = gun.Comp.Target,
                    };
                    RaiseLocalEvent(ent.Value, ref hitscanEv);

                    Del(ent);

                    Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                    break;
                default:
                    if (isServer)
                        throw new ArgumentOutOfRangeException();
                    break;
            }
        }

        if (isServer || shotProjectiles.Count > 0)
        {
            RaiseLocalEvent(gun, new AmmoShotEvent()
            {
                FiredProjectiles = shotProjectiles,
            });
        }

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gun, ref spreadEvent);

                var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2, ammoSpreadComp.Count);

                FireProjectile(ammoEnt, angles[0].ToVec());

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    FireProjectile(Spawn(ammoSpreadComp.Proto, fromEnt), angles[i].ToVec());
                }
            }
            else
            {
                FireProjectile(ammoEnt, mapDirection);
            }

            MuzzleFlash(gun, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
        }

        void FireProjectile(EntityUid projectile, Vector2 direction)
        {
            var projectileIndex = index++;
            var isProjectile = HasComp<ProjectileComponent>(projectile);

            // Throwing isn't predicted, so the client drops its copy and shows the server's.
            if (!isServer && !isProjectile)
            {
                Del(projectile);
                return;
            }

            ShootOrThrow(projectile, direction, gunVelocity, gun, user);
            shotProjectiles.Add(projectile);

            if (isProjectile)
                _gunPrediction.MarkFired(projectile, gun, user, projectileIndex);
        }
    }

    private void ShootOrThrow(EntityUid uid, Vector2 mapDirection, Vector2 gunVelocity, Entity<GunComponent> gun, EntityUid? user)
    {
        if (gun.Comp.Target is { } target && !TerminatingOrDeleted(target))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Target = target;
            Dirty(uid, targeted);
        }

        // Do a throw
        if (!HasComp<ProjectileComponent>(uid))
        {
            RemoveShootable(uid);
            // TODO: Someone can probably yeet this a billion miles so need to pre-validate input somewhere up the call stack.
            ThrowingSystem.TryThrow(uid, mapDirection, gun.Comp.ProjectileSpeedModified, user);
            return;
        }

        ShootProjectile(uid, mapDirection, gunVelocity, gun, user, gun.Comp.ProjectileSpeedModified);
    }

    /// <summary>
    /// Gets a linear spread of angles between start and end.
    /// </summary>
    private Angle[] LinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }

    private Angle GetRecoilAngle(Entity<GunComponent> gun, Angle direction)
    {
        var component = gun.Comp;
        var timeSinceLastFire = (Timing.CurTime - component.LastFire).TotalSeconds;
        var newTheta = MathHelper.Clamp(component.CurrentAngle.Theta + component.AngleIncreaseModified.Theta - component.AngleDecayModified.Theta * timeSinceLastFire, component.MinAngleModified.Theta, component.MaxAngleModified.Theta);
        component.CurrentAngle = new Angle(newTheta);
        component.LastFire = component.NextFire;
        DirtyField(gun.AsNullable(), nameof(GunComponent.CurrentAngle));
        DirtyField(gun.AsNullable(), nameof(GunComponent.LastFire));

        // Convert it so angle can go either side. Predicted so the client and server agree on a predicted shot.
        var random = SharedRandomExtensions.PredictedRandom(Timing, GetNetEntity(gun)).NextFloat(-0.5f, 0.5f);
        var spread = component.CurrentAngle.Theta * random;
        var angle = new Angle(direction.Theta + component.CurrentAngle.Theta * random);
        DebugTools.Assert(spread <= component.MaxAngleModified.Theta);
        return angle;
    }

    private void Recoil(EntityUid? user, Vector2 recoil, float recoilScalar)
    {
        if (_netManager.IsServer || !Timing.IsFirstTimePredicted || user == null || recoil == Vector2.Zero || recoilScalar == 0)
            return;

        // ES START
        // reduce recoil even further
        _cameraRecoil.KickCamera(user.Value, recoil.Normalized() * 0.13f * recoilScalar);
        // ES END
    }

    /// <summary>
    /// Plays the sound of something being hit by a projectile. The client only plays it when it's predicting the hit
    /// for the user, who the server then leaves out.
    /// </summary>
    public void PlayImpactSound(
        EntityUid otherEntity,
        DamageSpecifier? modifiedDamage,
        SoundSpecifier? weaponSound,
        bool forceWeaponSound,
        EntityUid? predictingUser = null)
    {
        if (_netManager.IsClient && predictingUser == null)
            return;

        DebugTools.Assert(!Deleted(otherEntity), "Impact sound entity was deleted");

        // Like projectiles and melee,
        // 1. Entity specific sound
        // 2. Ammo's sound
        // 3. Nothing
        if (!forceWeaponSound && modifiedDamage != null && modifiedDamage.GetTotal() > 0 && TryComp<RangedDamageSoundComponent>(otherEntity, out var rangedSound))
        {
            var type = SharedMeleeWeaponSystem.GetHighestDamageSound(modifiedDamage, ProtoMan);

            if (type != null && rangedSound.SoundTypes?.TryGetValue(type, out var damageSoundType) == true)
            {
                var damageSoundTypeParams = damageSoundType?.Params ?? AudioParams.Default;
                Audio.PlayPredicted(damageSoundType, otherEntity, predictingUser, damageSoundTypeParams.WithVariation(MeleeSoundSystem.DamagePitchVariation));
                return;
            }

            if (type != null && rangedSound.SoundGroups?.TryGetValue(type, out var damageSoundGroup) == true)
            {
                var damageSoundGroupParams = damageSoundGroup?.Params ?? AudioParams.Default;
                Audio.PlayPredicted(damageSoundGroup, otherEntity, predictingUser, damageSoundGroupParams.WithVariation(MeleeSoundSystem.DamagePitchVariation));
                return;
            }
        }

        if (weaponSound != null)
            Audio.PlayPredicted(weaponSound, otherEntity, predictingUser);
    }
}
