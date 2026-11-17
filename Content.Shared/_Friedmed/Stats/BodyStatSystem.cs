using Content.Shared.Body;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Friedmed.Stats;

public sealed partial class BodyStatSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetAllBodyStatsEvent>(_body.RelayEvent);
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
    private void OnStartup(Entity<BodyStatsComponent> ent, ref ComponentStartup args)
    {
        RefreshStats(ent.AsNullable());
    }

    [SubscribeLocalEvent]
    private void OnOrganInserted(Entity<BodyStatsComponent> ent, ref OrganInsertedIntoEvent args)
    {
        RefreshStats(ent.AsNullable());
    }

    [SubscribeLocalEvent]
    private void OnOrganRemoved(Entity<BodyStatsComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        RefreshStats(ent.AsNullable());
    }

    /// Recomputes the body's stats from its organs; call whenever a contribution changes.
    [PublicAPI]
    public void RefreshStats(Entity<BodyStatsComponent?> body)
    {
        if (!Resolve(body, ref body.Comp, false))
            return;

        var ev = new GetAllBodyStatsEvent(new Dictionary<ProtoId<BodyStatPrototype>, float>());
        RaiseLocalEvent(body, ref ev);
        foreach (var (stat, value) in ev.Stats)
        {
            if (ProtoMan.Resolve(stat, out var proto))
                ev.Stats[stat] = value / proto.Baseline;
        }

        if (ev.Stats.DictionaryEquals(body.Comp.Stats))
            return;

        body.Comp.Stats = ev.Stats;
        var changed = new BodyStatsChangedEvent(ev.Stats);
        RaiseLocalEvent(body, ref changed);
    }

    /// Each stat's effectiveness (summed contributions / baseline); stats nothing contributes to are left out.
    [PublicAPI]
    public Dictionary<ProtoId<BodyStatPrototype>, float> GetStats(Entity<BodyStatsComponent?> body)
    {
        return Resolve(body, ref body.Comp, false)
            ? new Dictionary<ProtoId<BodyStatPrototype>, float>(body.Comp.Stats)
            : new();
    }

    [PublicAPI]
    public float GetStat(Entity<BodyStatsComponent?> body, ProtoId<BodyStatPrototype> stat)
    {
        return Resolve(body, ref body.Comp, false) ? body.Comp.Stats.GetValueOrDefault(stat) : 1f;
    }
}
