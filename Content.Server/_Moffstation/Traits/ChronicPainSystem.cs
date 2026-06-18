using Content.Server.Stunnable;
using Content.Shared._Moffstation.Traits.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Traits;

public sealed partial class ChronicPainSystem : EntitySystem
{
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StatusEffectsSystem _statusEffect = default!;

    private static readonly EntProtoId PainNumbnessEffect = "StatusEffectPainNumbness";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChronicPainComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<ChronicPainComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Timer = (float) ent.Comp.WithdrawalInterval.TotalSeconds;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ChronicPainComponent>();
        while (query.MoveNext(out var uid, out var pain))
        {
            pain.Timer -= frameTime;
            if (pain.Timer > 0f)
                continue;

            pain.Timer = (float) pain.WithdrawalInterval.TotalSeconds;

            if (!_mobState.IsAlive(uid))
                continue;

            // Soretizone (or the PainNumbness trait) suppresses chronic pain episodes.
            if (_statusEffect.HasStatusEffect(uid, PainNumbnessEffect))
                continue;

            _stun.TryAddParalyzeDuration(uid, pain.StunDuration);
        }
    }
}
