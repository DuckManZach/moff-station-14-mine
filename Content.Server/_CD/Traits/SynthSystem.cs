using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server._CD.Traits;

/// <summary>
/// This basically replaces their blood. Overriding the blood component with the traits effect doesn't work because
/// the blood is already in... and for some reason doesnt get replaced. Whatever, this should make it work
/// </summary>
public sealed partial class SynthSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SynthComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<SynthComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<BloodstreamComponent>(ent.Owner, out var bloodstream))
            return;

        var solution = new Solution([new ReagentQuantity(ent.Comp.BloodReagentPrototype, bloodstream.BloodReferenceSolution.Volume)]);
        _bloodstream.ChangeBloodReagents(ent.Owner, solution);
    }
}
