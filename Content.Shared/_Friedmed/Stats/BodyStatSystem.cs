using Content.Shared._Friedmed.Wounds;
using Content.Shared._Moffstation.Extensions;
using Content.Shared.Body;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Stats;

public sealed partial class BodyStatSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetAllBodyStatsEvent>(_body.RelayEvent);
        SubscribeLocalEvent<BodyComponent, GetBodyStatEvent>(_body.RelayEvent);
    }

    [SubscribeLocalEvent]
    private static void OnGetAllBodyStats(Entity<BodyStatContributorComponent> ent, ref BodyRelayedEvent<GetAllBodyStatsEvent> args)
    {
        foreach (var (stat, value) in ent.Comp.Stats)
        {
            args.Args.Stats[stat] = args.Args.Stats.GetValueOrDefault(stat) + value;
        }
    }

    [SubscribeLocalEvent]
    private static void OnGetBodyStat(Entity<BodyStatContributorComponent> ent, ref BodyRelayedEvent<GetBodyStatEvent> args)
    {
        if (ent.Comp.Stats.TryGetValue(args.Args.StatId, out var value))
            args.Args = args.Args with { Value = args.Args.Value + value };
    }

    /// Each stat's summed contributions; stats nothing contributes to are left out.
    [PublicAPI]
    public Dictionary<ProtoId<BodyStatPrototype>, float> GetStats(Entity<WoundableBodyComponent?> body)
    {
        var ev = new GetAllBodyStatsEvent(new Dictionary<ProtoId<BodyStatPrototype>, float>());
        if (!Resolve(body, ref body.Comp, false))
            return ev.Stats;

        RaiseLocalEvent(body, ref ev);
        return ev.Stats;
    }

    [PublicAPI]
    public float GetStat(Entity<WoundableBodyComponent?> body, ProtoId<BodyStatPrototype> stat)
    {
        if (!Resolve(body, ref body.Comp, false))
            return 1f;

        var ev = new GetBodyStatEvent(stat);
        RaiseLocalEvent(body, ref ev);
        return ev.Value;
    }
}
