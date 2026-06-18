using Content.Shared._Moffstation.Traits.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Server._Moffstation.Traits;

public sealed partial class UnborgableSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UnborgableComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<MMIComponent, ItemSlotInsertAttemptEvent>(OnMMIInsertAttempt);
    }

    /// <summary>
    /// When the brain is removed from a body with UnborgableComponent, propagate the component to the brain.
    /// </summary>
    private void OnOrganRemoved(Entity<UnborgableComponent> body, ref OrganRemovedFromEvent args)
    {
        if (HasComp<BrainComponent>(args.Organ))
            EnsureComp<UnborgableComponent>(args.Organ);
    }

    private void OnMMIInsertAttempt(Entity<MMIComponent> mmi, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_itemSlots.TryGetSlot(mmi.Owner, mmi.Comp.BrainSlotId, out var brainSlot) || brainSlot != args.Slot)
            return;

        if (HasComp<UnborgableComponent>(args.Item))
            args.Cancelled = true;
    }
}
