using Content.Shared._Friedmed.Organs;
using Content.Shared.Body;
using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Friedmed.Stats;

public sealed partial class BodyStatSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BodySystem _body = default!;

    [Dependency] private EntityQuery<DamageableOrganComponent> _damageableOrganQuery;
    [Dependency] private EntityQuery<OrganComponent> _organQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetAllBodyStatsEvent>(_body.RelayEvent);
    }

    [SubscribeLocalEvent]
    private void OnGetAllBodyStats(Entity<BodyStatContributorComponent> ent, ref BodyRelayedEvent<GetAllBodyStatsEvent> args)
    {
        // A damaged organ contributes in proportion to its remaining health.
        var health = _damageableOrganQuery.TryComp(ent, out var damageable)
            ? 1f - damageable.Damage.Float() / damageable.MaxDamage.Float()
            : 1f;

        foreach (var (stat, value) in ent.Comp.Stats)
        {
            args.Args.Stats[stat] = args.Args.Stats.GetValueOrDefault(stat) + value * health;
        }
    }

    [SubscribeLocalEvent]
    private void OnOrganDamageChanged(Entity<BodyStatContributorComponent> ent, ref OrganDamageChangedEvent args)
    {
        if (_organQuery.CompOrNull(ent)?.Body is { } body)
            RefreshStats(body);
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

    [SubscribeLocalEvent]
    private static void OnGetState(Entity<BodyStatsComponent> ent, ref ComponentGetState args)
    {
        args.State = new BodyStatsComponentState(ent.Comp.Stats);
    }

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<BodyStatsComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not BodyStatsComponentState state || state.Stats.DictionaryEquals(ent.Comp.Stats))
            return;

        ent.Comp.Stats = new Dictionary<ProtoId<BodyStatPrototype>, float>(state.Stats);
        var changed = new BodyStatsChangedEvent(ent.Comp.Stats);
        RaiseLocalEvent(ent, ref changed);
    }

    /// Recomputes the body's stats from its organs; call whenever a contribution changes.
    [PublicAPI]
    public void RefreshStats(Entity<BodyStatsComponent?> body)
    {
        if (_timing.ApplyingState || !Resolve(body, ref body.Comp, false))
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
        Dirty(body);
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
