using System.Linq;
using Content.Server._Moffstation.Objectives.Components.AdLibs;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server._Moffstation.Objectives.Systems.AdLibs;

public sealed class AdLibObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<AdLibObjectiveComponent, ObjectiveAssignedEvent>(OnAssigned);
        // SubscribeLocalEvent<AdLibObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<AdLibObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<AdLibObjectiveComponent> ent, ref ObjectiveAssignedEvent args)
    {
        var stringArgs = ent.Comp.FillIns.Keys
            .Select(x => (x, (object)ent.Comp.DefaultFillIn))
            .ToArray();

        var title = Loc.GetString(ent.Comp.ObjectiveText, stringArgs);
        var desc = Loc.GetString(ent.Comp.DescriptionText, stringArgs);

        _metaData.SetEntityName(ent.Owner, title);
        _metaData.SetEntityDescription(ent.Owner, desc);
    }

    // private void OnAfterAssign(Entity<AdLibObjectiveComponent> ent, ref ObjectiveAfterAssignEvent args)
    // {
    //     var title = Loc.GetString(ent.Comp.ObjectiveText);
    //
    //     var description = Loc.GetString(ent.Comp.DescriptionText);
    //
    //     _metaData.SetEntityName(ent.Owner, title, args.Meta);
    //     _metaData.SetEntityDescription(ent.Owner, description, args.Meta);
    //     // _objectives.SetIcon(ent.Owner, group.Sprite, args.Objective);
    // }

    private static void OnGetProgress(Entity<AdLibObjectiveComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0f;
    }
}
