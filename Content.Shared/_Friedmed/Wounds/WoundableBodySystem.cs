using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Friedmed.Organs;
using Content.Shared._Moffstation.Extensions;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.FixedPoint;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Friedmed.Wounds;

public sealed partial class WoundableBodySystem : OffbrandDamageSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private WoundableOrganSystem _woundableOrgan = default!;
    [Dependency] private WoundableSystem _woundable = default!;

    [Dependency] private EntityQuery<CollateralWoundTargetComponent> _collateralQuery;
    [Dependency] private EntityQuery<ParentOrganComponent> _parentOrganQuery;

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<WoundableBodyComponent> ent, ref ComponentShutdown args)
    {
        if (!_statusEffects.TryEffectsWithComp<WoundComponent>(ent, out var wounds))
            return;

        foreach (var wound in wounds)
        {
            QueueDel(wound);
        }
    }

    public void HealWounds(Entity<WoundableBodyComponent> ent, DamageSpecifier incoming, bool passive, bool refresh)
    {
        var evt = new HealWoundsEvent(incoming, passive);
        RaiseLocalEvent(ent, ref evt);

        if (refresh)
            _woundable.RefreshWounds(ent, false, null);
    }

    [SubscribeLocalEvent]
    private void OnDamageDealt(Entity<WoundableBodyComponent> ent, ref DamageDealtEvent args)
    {
        if (_timing.ApplyingState || !TryComp<DamageableComponent>(ent, out _))
            return;

        if (args.Damage.AnyPositive())
        {
            var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent));

            var organs = _woundableOrgan.GetWoundTargets(ent);
            var target = SharedRandomExtensions.Pick(organs, rand);
            var damage = DamageSpecifier.GetPositive(args.Damage);

            if (damage.GetTotal() >= target.Comp.CollateralThreshold &&
                rand.Prob(target.Comp.CollateralChance) &&
                _parentOrganQuery.TryComp(target, out var parentOrganComponent))
            {
                var collateralOptions = _collateralQuery.ResolveAll(parentOrganComponent.Children, logMissing: false)
                    .ToDictionary(o => o, o => o.Comp.Weight);

                if (collateralOptions.Count > 0)
                {
                    var collateral = SharedRandomExtensions.Pick(collateralOptions, rand);

                    var collateralEv = args with { Damage = damage * collateral.Comp.DamageShareRange.NextFloat(rand) };
                    RaiseLocalEvent(collateral, ref collateralEv);
                    damage -= collateralEv.Damage;
                }
            }

            var organEv = args with { Damage = damage };
            RaiseLocalEvent(target, ref organEv);
        }

        if (args.Damage.AnyNegative())
            HealWounds(ent, DamageSpecifier.GetNegative(args.Damage), false, false);

        _woundable.RefreshWounds(ent, args.InterruptsDoAfters, args.Origin);
    }

    [SubscribeLocalEvent]
    private void OnRefreshWounds(Entity<WoundableBodyComponent> ent, ref RefreshWoundsEvent args)
    {
        var damageable = Comp<DamageableComponent>(ent);

        var evt = new WoundGetDamageEvent(new(), null);
        RaiseLocalEvent(ent, ref evt);

        var dict = damageable.Damage.DamageDict;

        var damageDone = new DamageSpecifier();
        foreach (var (type, newValue) in evt.Accumulator.DamageDict)
        {
            var oldValue = dict.GetValueOrDefault(type, FixedPoint2.Zero);

            damageDone.DamageDict[type] = newValue - oldValue;
        }

        damageable.Damage = evt.Accumulator;
        _damageable.OnEntityDamageChanged((ent, damageable), damageDone, args.InterruptsDoAfters, args.Origin);
    }

    public void ClampWounds(Entity<WoundableBodyComponent> ent, float probability)
    {
        var evt = new ClampWoundsEvent(probability);
        RaiseLocalEvent(ent, ref evt);
    }
}
