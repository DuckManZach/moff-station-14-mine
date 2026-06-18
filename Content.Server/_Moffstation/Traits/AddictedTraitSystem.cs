using Content.Server.Stunnable;
using Content.Shared._Moffstation.Traits.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Moffstation.Traits;

public sealed partial class AddictedTraitSystem : EntitySystem
{
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddictedTraitComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<AddictedTraitComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Timer = (float) ent.Comp.WithdrawalInterval.TotalSeconds;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AddictedTraitComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var addicted, out var bloodstream))
        {
            addicted.Timer -= frameTime;
            if (addicted.Timer > 0f)
                continue;

            addicted.Timer = (float) addicted.WithdrawalInterval.TotalSeconds;

            if (!_mobState.IsAlive(uid))
                continue;

            // If the entity currently has the substance in their metabolites, no withdrawal.
            Entity<SolutionComponent>? metabolitesSolutionEnt = null;
            if (_solutionContainer.ResolveSolution(uid, bloodstream.MetabolitesSolutionName, ref metabolitesSolutionEnt, out var metabolites)
                && metabolites.ContainsReagent(new ReagentId(addicted.Substance, null)))
            {
                continue;
            }

            _stun.TryAddStunDuration(uid, addicted.StunDuration);
        }
    }
}
