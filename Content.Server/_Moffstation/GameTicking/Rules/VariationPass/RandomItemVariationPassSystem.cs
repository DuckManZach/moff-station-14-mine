using Content.Server.CrewManifest;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.VariationPass;
using Content.Server.GameTicking.Rules.VariationPass.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.GameTicking.Rules.VariationPass;

public sealed class RandomItemVariationPassSystem : VariationPassSystem<RandomItemVariationPassComponent>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CrewManifestSystem _crewManifest = default!;

    protected override void ApplyVariation(Entity<RandomItemVariationPassComponent> ent, ref StationVariationPassEvent args)
    {
        var players = AllEntityQuery<HumanoidAppearanceComponent, ActorComponent, MobStateComponent, TransformComponent>();
    }
}
