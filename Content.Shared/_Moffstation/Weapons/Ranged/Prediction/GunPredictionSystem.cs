using Content.Shared._Moffstation.CCVar;
using Robust.Shared.Configuration;

namespace Content.Shared._Moffstation.Weapons.Ranged.Prediction;

/// <summary>
/// Lets the shooter's client spawn its own projectiles and report their hits, which the server checks against
/// lag-compensated positions. A projectile is identified on both sides by its gun, the tick it was fired on and its
/// index among the projectiles fired that tick.
/// </summary>
public abstract partial class GunPredictionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;

    public bool Enabled { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, MoffCCVars.GunPrediction, v => Enabled = v, true);
    }

    /// <summary>
    /// Whether this side should spawn its own projectiles for a shot by the user.
    /// </summary>
    public abstract bool ShouldPredictShot(EntityUid? user);

    public abstract void OnProjectileFired(EntityUid projectile, EntityUid gun, EntityUid? user, int index);
}
