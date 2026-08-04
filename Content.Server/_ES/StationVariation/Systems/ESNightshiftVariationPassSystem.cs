using Content.Server._ES.StationVariation.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.VariationPass;
using Content.Server.Light.EntitySystems;
using Content.Shared.Light.Components;

namespace Content.Server._ES.StationVariation.Systems;

/// <inheritdoc cref="ESNightshiftVariationPassComponent"/>
public sealed partial class ESNightshiftVariationPassSystem : VariationPassSystem<ESNightshiftVariationPassComponent>
{
    [Dependency] private PoweredLightSystem _poweredLight = default!;

    protected override void ApplyVariation(Entity<ESNightshiftVariationPassComponent> ent, ref StationVariationPassEvent args)
    {
        var query = AllEntityQuery<PoweredLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var light, out var xform))
        {
            if (!IsMemberOfStation((uid, xform), ref args))
                continue;

            if (light.HasLampOnSpawn is not { } lamp)
                continue;

            if (!ent.Comp.LampReplacements.TryGetValue(lamp, out var replacement))
                continue;

            _poweredLight.ReplaceSpawnedPrototype((uid, light), replacement);
        }
    }
}
