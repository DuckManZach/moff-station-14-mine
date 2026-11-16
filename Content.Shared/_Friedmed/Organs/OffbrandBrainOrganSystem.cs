using Content.Shared._Friedmed.Wounds;
using Content.Shared.Body;

namespace Content.Shared._Friedmed.Organs;

public sealed partial class OffbrandBrainOrganSystem : EntitySystem
{
    [Dependency] private BrainDamageThresholdsSystem _thresholds = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OffbrandBrainOrganComponent, OrganDamageChangedEvent>(OnOrganDamageChanged);
        SubscribeLocalEvent<OffbrandBrainOrganComponent, OrganGotInsertedEvent>(OnGotInserted);
    }

    private void OnOrganDamageChanged(Entity<OffbrandBrainOrganComponent> ent, ref OrganDamageChangedEvent args)
    {
        if (Comp<OrganComponent>(ent).Body is { } body)
            _thresholds.OnAfterBrainDamageChanged(body, (ent, Comp<DamageableOrganComponent>(ent)));
    }

    private void OnGotInserted(Entity<OffbrandBrainOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        _thresholds.OnAfterBrainDamageChanged(args.Target, (ent, Comp<DamageableOrganComponent>(ent)));
    }
}
