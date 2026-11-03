using System.Linq;
using Content.Shared._Friedmed.OrganVisuals;
using Content.Shared._Friedmed.Organs;
using Content.Shared._Friedmed.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Analyzers;

public sealed partial class VitalsAnalyzerSystem : EntitySystem
{
    [Dependency] private PerfusionSystem _perfusion = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private MetabolizerSystem _metabolizer = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private WoundableSystem _woundable = default!;

    private const string MedicineGroup = "Medicine";
    private static readonly ProtoId<MetabolismStagePrototype> MetabolitesStage = "Metabolites";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VitalsAnalyzerComponent, AnalyzerUpdatedEvent>(OnAnalyzerUpdated);
    }

    private void OnAnalyzerUpdated(Entity<VitalsAnalyzerComponent> ent, ref AnalyzerUpdatedEvent args)
    {
        ent.Comp.Data = TakeSample(args.Target);
        Dirty(ent);
    }

    private Dictionary<ProtoId<ReagentPrototype>, (FixedPoint2 InBloodstream, FixedPoint2 Metabolites)>? SampleReagents(EntityUid uid, out bool hasNonMedical)
    {
        hasNonMedical = false;
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return null;

        if (!_solutionContainer.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution))
            return null;

        var ret = new Dictionary<ProtoId<ReagentPrototype>, (FixedPoint2 InBloodstream, FixedPoint2 Metabolites)>();
        var reference = bloodstream.BloodReferenceSolution;

        foreach (var (reagentId, quantity) in bloodstream.BloodSolution.Value.Comp.Solution.Contents)
        {
            if (reference.ContainsPrototype(reagentId.Prototype))
                continue;

            ProtoId<ReagentPrototype> reagent = reagentId.Prototype;

            if (_prototype.Index(reagent).Group != MedicineGroup)
            {
                hasNonMedical = true;
                continue;
            }

            if (!ret.ContainsKey(reagent))
                ret[reagent] = (0, 0);

            ret[reagent] = (ret[reagent].InBloodstream + quantity, ret[reagent].Metabolites);
        }

        _body.TryGetOrgansWithComponent<MetabolizerComponent>(uid, out var metabolizers);
        foreach (var metabolizer in metabolizers)
        {
            var solutions = metabolizer.Comp.Solutions;
            var stages = metabolizer.Comp.Stages;
            if (!solutions.TryGetValue(MetabolitesStage, out var stage) || !stages.Contains(MetabolitesStage))
                continue;

            if (!_metabolizer.LookupSolution(metabolizer, stage, false, out var metabolitesSolution, out _, out _))
                continue;

            foreach (var (reagent, quantity) in metabolitesSolution.Contents)
            {
                if (_prototype.Index<ReagentPrototype>(reagent.Prototype).Group != MedicineGroup)
                {
                    hasNonMedical = true;
                    continue;
                }

                if (!ret.ContainsKey(reagent.Prototype))
                    ret[reagent.Prototype] = (0, 0);

                ret[reagent.Prototype] = (ret[reagent.Prototype].InBloodstream, ret[reagent.Prototype].Metabolites + quantity);
            }
        }

        return ret;
    }

    private List<BodyPartData> SampleBodyParts(EntityUid uid)
    {
        var parts = new List<BodyPartData>();
        var present = new HashSet<ProtoId<OrganCategoryPrototype>>();

        foreach (var (organ, organComp, metaData) in _body.EnumerateOrgans<MetaDataComponent>(uid))
        {
            if (organComp.Category is { } category)
                present.Add(category);

            var health = SampleOrganHealth(organ);
            var conditions = SampleConditions(organ);

            if (metaData.EntityPrototype is not { } prototype || health >= 1f && conditions.Count == 0)
                continue;

            parts.Add(new BodyPartData { Organ = prototype.ID, Health = health, Conditions = conditions });
        }

        if (!TryComp<InitialBodyComponent>(uid, out var initialBody))
            return parts;

        foreach (var (category, organ) in initialBody.Organs)
        {
            if (!present.Contains(category))
                parts.Add(new BodyPartData { Organ = organ, Missing = true });
        }

        return parts;
    }

    private float SampleOrganHealth(EntityUid organ)
    {
        var health = 1f;

        if (TryComp<DamageableOrganComponent>(organ, out var damageable))
            health = 1f - damageable.Damage.Float() / damageable.MaxDamage.Float();

        // Wounds have no hard cap on a part, so the heaviest wound visual stands in for it.
        if (TryComp<WoundableComponent>(organ, out var woundable) &&
            TryComp<VisualOrganWoundsComponent>(organ, out var visuals) &&
            visuals.Thresholds.Count != 0)
        {
            health = MathF.Min(health, 1f - woundable.Damage.GetTotal().Float() / visuals.Thresholds[^1].Float());
        }

        return Math.Clamp(health, 0f, 1f);
    }

    private List<BodyConditionData> SampleConditions(EntityUid organ)
    {
        if (!_statusEffects.TryEffectsWithComp<WoundComponent>(organ, out var wounds))
            return new();

        var conditions = new Dictionary<(EntProtoId, bool, bool), BodyConditionData>();

        foreach (var wound in wounds)
        {
            if (Prototype(wound) is not { } prototype)
                continue;

            var damage = wound.Comp1.Damage.GetTotal();
            var bleedLevel = TryComp<BleedingWoundComponent>(wound, out var bleedingWound)
                ? _woundable.BleedLevel((wound.Owner, bleedingWound))
                : 0f;
            var bleeding = bleedLevel > 0f;
            var tended = TryComp<TendableWoundComponent>(wound, out var tendable) && tendable.Tended;

            if (conditions.TryGetValue((prototype.ID, bleeding, tended), out var condition))
            {
                condition.Count++;
                condition.Damage += damage;
                condition.BleedLevel += bleedLevel;
                continue;
            }

            conditions[(prototype.ID, bleeding, tended)] = new BodyConditionData
            {
                Wound = prototype.ID,
                Count = 1,
                Damage = damage,
                Description = CompOrNull<AnalyzableWoundComponent>(wound)?.Descriptions.HighestMatch(damage)
                              ?? CompOrNull<WoundDescriptionComponent>(wound)?.Descriptions.HighestMatch(damage),
                BleedLevel = bleedLevel,
                Tended = tended,
            };
        }

        return conditions.Values
            .OrderByDescending(condition => condition.Damage)
            .ThenBy(condition => condition.Wound.Id)
            .ToList();
    }

    private float SamplePain(EntityUid uid)
    {
        if (!TryComp<ShockThresholdsComponent>(uid, out var thresholds) || thresholds.Thresholds.Count == 0)
            return 0f;

        return _pain.GetShock(uid).Float() / thresholds.Thresholds.Keys.Last().Float();
    }

    public VitalsData? TakeSample(EntityUid uid, bool withWounds = true)
    {
        if (!TryComp<PerfusionComponent>(uid, out var heartrate))
            return null;

        if (!TryComp<BrainDamageThresholdsComponent>(uid, out var brainDamageThresholds))
            return null;

        var (upper, lower) = _perfusion.BloodPressure((uid, heartrate));

        var hasNonMedical = false;
        var reagents = withWounds ? SampleReagents(uid, out hasNonMedical) : null;

        return new VitalsData()
            {
                BrainHealth = 1f - brainDamageThresholds.DisplayDamage.Float() / brainDamageThresholds.DisplayMaxDamage.Float(),
                BloodPressure = (upper, lower),
                HeartRate = _perfusion.HeartRate((uid, heartrate)),
                HeartStrain = heartrate.Strain,
                Etco2 = _perfusion.Etco2((uid, heartrate)),
                RespiratoryRate = _perfusion.RespiratoryRate((uid, heartrate)),
                RespiratoryRateModifier = _perfusion.ComputeRespiratoryRateModifier((uid, heartrate)),
                Spo2 = _perfusion.Spo2((uid, heartrate)).Float(),
                Etco2Name = heartrate.Etco2Name,
                Etco2GasName = heartrate.Etco2GasName,
                Spo2Name = heartrate.Spo2Name,
                Spo2GasName = heartrate.Spo2GasName,
                Reagents = reagents,
                NonMedicalReagents = hasNonMedical,
                BloodLevel = _bloodstream.GetBloodLevel(uid),
                Pain = SamplePain(uid),
                PainStatus = CompOrNull<ShockThresholdsComponent>(uid)?.CurrentThresholdState,
                BodyParts = withWounds ? SampleBodyParts(uid) : null,
            };
    }
}
