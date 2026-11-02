using Content.Server.Construction;
using Content.Shared._Friedmed.Surgery;
using Robust.Shared.Prototypes;

namespace Content.Server._Friedmed.Surgery;

public sealed partial class SurgeryGuideTargetSystem : SharedSurgeryGuideTargetSystem
{
    [Dependency] private ConstructionSystem _construction = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    protected override void OnStartSurgery(Entity<SurgeryGuideTargetComponent> ent, ref SurgeryGuideStartSurgeryMessage args)
    {
        base.OnStartSurgery(ent, ref args);
        if (!_prototype.Resolve(args.Prototype, out var construction))
            return;

        _construction.SetPathfindingTarget(ent, construction.TargetNode);
    }

    protected override void OnStartCleanup(Entity<SurgeryGuideTargetComponent> ent, ref SurgeryGuideStartCleanupMessage args)
    {
        base.OnStartCleanup(ent, ref args);
        _construction.SetPathfindingTarget(ent, "Base");
    }
}
