using Content.Shared._Moffstation.Extensions;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Stats;

public sealed partial class BodyStatSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetBodyStatsEvent>(_body.RelayEvent);
    }

    [SubscribeLocalEvent]
    private void OnGetBodyStats(Entity<BodyStatContributorComponent> ent, ref BodyRelayedEvent<GetBodyStatsEvent> args)
    {
        foreach (var (stat, value) in ent.Comp.Stats)
        {
            args.Args.Stats[stat] = args.Args.Stats.GetValueOrDefault(stat) + value;
        }
    }

    /// Each stat's summed contributions; stats nothing contributes to are left out.
    public Dictionary<ProtoId<BodyStatPrototype>, float> GetStats(EntityUid body)
    {
        var ev = new GetBodyStatsEvent(new());
        RaiseLocalEvent(body, ref ev);
        return ev.Stats;
    }
}
