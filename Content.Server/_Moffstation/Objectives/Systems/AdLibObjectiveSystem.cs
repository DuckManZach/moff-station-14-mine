using Content.Server._Moffstation.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using NetCord;

namespace Content.Server._Moffstation.Objectives.Systems;

/// <summary>
/// This handles...
/// </summary>
public sealed class AdLibObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<AdLibObjectiveComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<AdLibObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<AdLibObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<AdLibObjectiveComponent> ent, ref ObjectiveAssignedEvent args)
    {
        return;
    }

    private void OnAfterAssign(Entity<AdLibObjectiveComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        var title = Loc.GetString(ent.Comp.ObjectiveText);

        var description = Loc.GetString(ent.Comp.DescriptionText);

        _metaData.SetEntityName(ent.Owner, title, args.Meta);
        _metaData.SetEntityDescription(ent.Owner, description, args.Meta);
        // _objectives.SetIcon(ent.Owner, group.Sprite, args.Objective);
    }

    private static void OnGetProgress(Entity<AdLibObjectiveComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0f;
    }
}
