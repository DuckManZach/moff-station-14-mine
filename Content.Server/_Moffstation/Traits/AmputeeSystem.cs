using Content.Shared._Moffstation.Traits.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server._Moffstation.Traits;

public sealed partial class AmputeeSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AmputeeComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<AmputeeComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<HandsComponent>(ent.Owner, out var hands))
            return;

        foreach (var (handName, hand) in hands.Hands)
        {
            if (hand.Location != ent.Comp.Location)
                continue;

            _hands.RemoveHand(ent.Owner, handName);
            return;
        }
    }
}
