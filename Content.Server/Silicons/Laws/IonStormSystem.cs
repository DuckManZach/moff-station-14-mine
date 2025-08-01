using Content.Server.StationEvents.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Silicons.Laws;

public sealed class IonStormSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SiliconLawSystem _siliconLaw = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;

    // funny
    private static readonly ProtoId<DatasetPrototype> Threats = "IonStormThreats";
    private static readonly ProtoId<DatasetPrototype> Objects = "IonStormObjects";
    private static readonly ProtoId<DatasetPrototype> Crew = "IonStormCrew";
    private static readonly ProtoId<DatasetPrototype> Adjectives = "IonStormAdjectives";
    private static readonly ProtoId<DatasetPrototype> Verbs = "IonStormVerbs";
    private static readonly ProtoId<DatasetPrototype> NumberBase = "IonStormNumberBase";
    private static readonly ProtoId<DatasetPrototype> NumberMod = "IonStormNumberMod";
    private static readonly ProtoId<DatasetPrototype> Areas = "IonStormAreas";
    private static readonly ProtoId<DatasetPrototype> Feelings = "IonStormFeelings";
    private static readonly ProtoId<DatasetPrototype> FeelingsPlural = "IonStormFeelingsPlural";
    private static readonly ProtoId<DatasetPrototype> Musts = "IonStormMusts";
    private static readonly ProtoId<DatasetPrototype> Requires = "IonStormRequires";
    private static readonly ProtoId<DatasetPrototype> Actions = "IonStormActions";
    private static readonly ProtoId<DatasetPrototype> Allergies = "IonStormAllergies";
    private static readonly ProtoId<DatasetPrototype> AllergySeverities = "IonStormAllergySeverities";
    private static readonly ProtoId<DatasetPrototype> Concepts = "IonStormConcepts";
    private static readonly ProtoId<DatasetPrototype> Drinks = "IonStormDrinks";
    private static readonly ProtoId<DatasetPrototype> Foods = "IonStormFoods";

    /// <summary>
    /// Randomly alters the laws of an individual silicon.
    /// </summary>
    public void IonStormTarget(Entity<SiliconLawBoundComponent, IonStormTargetComponent> ent, bool adminlog = true)
    {
        var lawBound = ent.Comp1;
        var target = ent.Comp2;
        if (!_robustRandom.Prob(target.Chance))
            return;

        var laws = _siliconLaw.GetLaws(ent, lawBound);
        if (laws.Laws.Count == 0)
            return;

        // try to swap it out with a random lawset
        if (_robustRandom.Prob(target.RandomLawsetChance))
        {
            var lawsets = _proto.Index<WeightedRandomPrototype>(target.RandomLawsets);
            var lawset = lawsets.Pick(_robustRandom);
            laws = _siliconLaw.GetLawset(lawset);
        }
        // clone it so not modifying stations lawset
        laws = laws.Clone();

        // shuffle them all
        if (_robustRandom.Prob(target.ShuffleChance))
        {
            // hopefully work with existing glitched laws if there are multiple ion storms
            var baseOrder = FixedPoint2.New(1);
            foreach (var law in laws.Laws)
            {
                if (law.Order < baseOrder)
                    baseOrder = law.Order;
            }

            _robustRandom.Shuffle(laws.Laws);

            // change order based on shuffled position
            for (int i = 0; i < laws.Laws.Count; i++)
            {
                laws.Laws[i].Order = baseOrder + i;
            }
        }

        // see if we can remove a random law
        if (laws.Laws.Count > 0 && _robustRandom.Prob(target.RemoveChance))
        {
            var i = _robustRandom.Next(laws.Laws.Count);
            laws.Laws.RemoveAt(i);
        }

        // generate a new law...
        var newLaw = GenerateLaw();

        // see if the law we add will replace a random existing law or be a new glitched order one
        if (laws.Laws.Count > 0 && _robustRandom.Prob(target.ReplaceChance))
        {
            var i = _robustRandom.Next(laws.Laws.Count);
            laws.Laws[i] = new SiliconLaw()
            {
                LawString = newLaw,
                Order = laws.Laws[i].Order
            };
        }
        else
        {
            laws.Laws.Insert(0, new SiliconLaw
            {
                LawString = newLaw,
                Order = -1,
                LawIdentifierOverride = Loc.GetString("ion-storm-law-scrambled-number", ("length", _robustRandom.Next(5, 10)))
            });
        }

        // sets all unobfuscated laws' indentifier in order from highest to lowest priority
        // This could technically override the Obfuscation from the code above, but it seems unlikely enough to basically never happen
        int orderDeduction = -1;

        for (int i = 0; i < laws.Laws.Count; i++)
        {
            var notNullIdentifier = laws.Laws[i].LawIdentifierOverride ?? (i - orderDeduction).ToString();

            if (notNullIdentifier.Any(char.IsSymbol))
            {
                orderDeduction += 1;
            }
            else
            {
                laws.Laws[i].LawIdentifierOverride = (i - orderDeduction).ToString();
            }
        }

        // adminlog is used to prevent adminlog spam.
        if (adminlog)
            _adminLogger.Add(LogType.Mind, LogImpact.High, $"{ToPrettyString(ent):silicon} had its laws changed by an ion storm to {laws.LoggingString()}");

        // laws unique to this silicon, dont use station laws anymore
        EnsureComp<SiliconLawProviderComponent>(ent);
        var ev = new IonStormLawsEvent(laws);
        RaiseLocalEvent(ent, ref ev);
    }

    // for your own sake direct your eyes elsewhere
    private string GenerateLaw()
    {
        // pick all values ahead of time to make the logic cleaner
        var threats = Pick(Threats);
        var objects = Pick(Objects);
        var crew1 = Pick(Crew);
        var crew2 = Pick(Crew);
        var adjective = Pick(Adjectives);
        var verb = Pick(Verbs);
        var number = Pick(NumberBase) + " " + Pick(NumberMod);
        var area = Pick(Areas);
        var feeling = Pick(Feelings);
        var feelingPlural = Pick(FeelingsPlural);
        var must = Pick(Musts);
        var require = Pick(Requires);
        var action = Pick(Actions);
        var allergy = Pick(Allergies);
        var allergySeverity = Pick(AllergySeverities);
        var concept = Pick(Concepts);
        var drink = Pick(Drinks);
        var food = Pick(Foods);

        var joined = $"{number} {adjective}";
        // a lot of things have subjects of a threat/crew/object
        var triple = _robustRandom.Next(0, 3) switch
        {
            0 => threats,
            1 => crew1,
            2 => objects,
            _ => throw new IndexOutOfRangeException(),
        };
        var crewAll = _robustRandom.Prob(0.5f) ? crew2 : Loc.GetString("ion-storm-crew");
        var objectsThreats = _robustRandom.Prob(0.5f) ? objects : threats;
        var objectsConcept = _robustRandom.Prob(0.5f) ? objects : concept;
        // s goes ahead of require, is/are
        // i dont think theres a way to do this in fluent
        var (who, plural) = _robustRandom.Next(0, 5) switch
        {
            0 => (Loc.GetString("ion-storm-you"), true),
            1 => (Loc.GetString("ion-storm-the-station"), false),
            2 => (Loc.GetString("ion-storm-the-crew"), false),
            3 => (Loc.GetString("ion-storm-the-job", ("job", crew2)), true),
            _ => (area, false) // THE SINGULARITY REQUIRES THE HAPPY CLOWNS
        };
        var jobChange = _robustRandom.Next(0, 3) switch
        {
            0 => crew1,
            1 => Loc.GetString("ion-storm-clowns"),
            _ => Loc.GetString("ion-storm-heads")
        };
        var part = Loc.GetString("ion-storm-part", ("part", _robustRandom.Prob(0.5f)));
        var harm = _robustRandom.Next(0, 6) switch
        {
            0 => concept,
            1 => $"{adjective} {threats}",
            2 => $"{adjective} {objects}",
            3 => Loc.GetString("ion-storm-adjective-things", ("adjective", adjective)),
            4 => crew1,
            _ => Loc.GetString("ion-storm-x-and-y", ("x", crew1), ("y", crew2))
        };

        if (plural) feeling = feelingPlural;

        var subjects = _robustRandom.Prob(0.5f) ? objectsThreats : Loc.GetString("ion-storm-people");

        // message logic!!!
        return _robustRandom.Next(0, 35) switch
        {
            0  => Loc.GetString("ion-storm-law-on-station", ("joined", joined), ("subjects", triple)),
            1  => Loc.GetString("ion-storm-law-call-shuttle", ("joined", joined), ("subjects", triple)),
            2  => Loc.GetString("ion-storm-law-crew-are", ("who", crewAll), ("joined", joined), ("subjects", objectsThreats)),
            3  => Loc.GetString("ion-storm-law-subjects-harmful", ("adjective", adjective), ("subjects", triple)),
            4  => Loc.GetString("ion-storm-law-must-harmful", ("must", must)),
            5  => Loc.GetString("ion-storm-law-thing-harmful", ("thing", _robustRandom.Prob(0.5f) ? concept : action)),
            6  => Loc.GetString("ion-storm-law-job-harmful", ("adjective", adjective), ("job", crew1)),
            7  => Loc.GetString("ion-storm-law-having-harmful", ("adjective", adjective), ("thing", objectsConcept)),
            8  => Loc.GetString("ion-storm-law-not-having-harmful", ("adjective", adjective), ("thing", objectsConcept)),
            9  => Loc.GetString("ion-storm-law-requires", ("who", who), ("plural", plural), ("thing", _robustRandom.Prob(0.5f) ? concept : require)),
            10 => Loc.GetString("ion-storm-law-requires-subjects", ("who", who), ("plural", plural), ("joined", joined), ("subjects", triple)),
            11 => Loc.GetString("ion-storm-law-allergic", ("who", who), ("plural", plural), ("severity", allergySeverity), ("allergy", _robustRandom.Prob(0.5f) ? concept : allergy)),
            12 => Loc.GetString("ion-storm-law-allergic-subjects", ("who", who), ("plural", plural), ("severity", allergySeverity), ("adjective", adjective), ("subjects", _robustRandom.Prob(0.5f) ? objects : crew1)),
            13 => Loc.GetString("ion-storm-law-feeling", ("who", who), ("feeling", feeling), ("concept", concept)),
            14 => Loc.GetString("ion-storm-law-feeling-subjects", ("who", who), ("feeling", feeling), ("joined", joined), ("subjects", triple)),
            15 => Loc.GetString("ion-storm-law-you-are", ("concept", concept)),
            16 => Loc.GetString("ion-storm-law-you-are-subjects", ("joined", joined), ("subjects", triple)),
            17 => Loc.GetString("ion-storm-law-you-must-always", ("must", must)),
            18 => Loc.GetString("ion-storm-law-you-must-never", ("must", must)),
            19 => Loc.GetString("ion-storm-law-eat", ("who", crewAll), ("adjective", adjective), ("food", _robustRandom.Prob(0.5f) ? food : triple)),
            20 => Loc.GetString("ion-storm-law-drink", ("who", crewAll), ("adjective", adjective), ("drink", drink)),
            21 => Loc.GetString("ion-storm-law-change-job", ("who", crewAll), ("adjective", adjective), ("change", jobChange)),
            22 => Loc.GetString("ion-storm-law-highest-rank", ("who", crew1)),
            23 => Loc.GetString("ion-storm-law-lowest-rank", ("who", crew1)),
            24 => Loc.GetString("ion-storm-law-crew-must", ("who", crewAll), ("must", must)),
            25 => Loc.GetString("ion-storm-law-crew-must-go", ("who", crewAll), ("area", area)),
            26 => Loc.GetString("ion-storm-law-crew-only-1", ("who", crew1), ("part", part)),
            27 => Loc.GetString("ion-storm-law-crew-only-2", ("who", crew1), ("other", crew2), ("part", part)),
            28 => Loc.GetString("ion-storm-law-crew-only-subjects", ("adjective", adjective), ("subjects", subjects), ("part", part)),
            29 => Loc.GetString("ion-storm-law-crew-must-do", ("must", must), ("part", part)),
            30 => Loc.GetString("ion-storm-law-crew-must-have", ("adjective", adjective), ("objects", objects), ("part", part)),
            31 => Loc.GetString("ion-storm-law-crew-must-eat", ("who", who), ("adjective", adjective), ("food", food), ("part", part)),
            32 => Loc.GetString("ion-storm-law-harm", ("who", harm)),
            33 => Loc.GetString("ion-storm-law-protect", ("who", harm)),
            _ => Loc.GetString("ion-storm-law-concept-verb", ("concept", concept), ("verb", verb), ("subjects", triple))
        };
    }

    /// <summary>
    /// Picks a random value from an ion storm dataset.
    /// All ion storm datasets start with IonStorm.
    /// </summary>
    private string Pick(string name)
    {
        var dataset = _proto.Index<DatasetPrototype>(name);
        return _robustRandom.Pick(dataset.Values);
    }
}
