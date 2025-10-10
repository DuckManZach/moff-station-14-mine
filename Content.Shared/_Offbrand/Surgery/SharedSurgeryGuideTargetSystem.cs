using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Shared._Offbrand.Surgery;

public abstract class SharedSurgeryGuideTargetSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryToolComponent, GetVerbsEvent<UtilityVerb>>(OnGetVerbs);

        Subs.BuiEvents<SurgeryGuideTargetComponent>(SurgeryGuideUiKey.Key,
                sub =>
                {
                    sub.Event<SurgeryGuideStartSurgeryMessage>(OnStartSurgery);
                    sub.Event<SurgeryGuideStartCleanupMessage>(OnStartCleanup);
                    sub.Event<BoundUIClosedEvent>(OnClose);
                });
    }

    private void OnGetVerbs(Entity<SurgeryToolComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<SurgeryGuideTargetComponent>(args.Target, out var surgeryGuide))   // Moffstation - Fix double surgery glitch
            return;

        var @event = args;
        args.Verbs.Add(new UtilityVerb()
        {
            Act = () =>
            {
                if (surgeryGuide.SelectionOccupied)
                {
                    _popup.PopupPredictedCursor(Loc.GetString("popup-surgery-occupied"), @event.User);
                }
                else
                {
                    _userInterface.OpenUi(@event.Target, SurgeryGuideUiKey.Key, @event.User);
                    surgeryGuide.SelectionOccupied = true;  // Moffstation - Fix double surgery glitch
                }
            },
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")), // Moffstation - Give surgery verb an icon
            // Moffstation - Start - Fix double surgery glitch
            Text = Loc.GetString(surgeryGuide.SelectionOccupied ? "verb-surgery-occupied" : "verb-perform-surgery"),
            Disabled = surgeryGuide.SelectionOccupied
            // Moffstation - End
        });
    }

    protected virtual void OnStartSurgery(Entity<SurgeryGuideTargetComponent> ent, ref SurgeryGuideStartSurgeryMessage args)
    {
        _userInterface.CloseUi(ent.Owner, SurgeryGuideUiKey.Key, args.Actor);
        _popup.PopupPredictedCursor(Loc.GetString("surgery-examine-for-instructions"), args.Actor);
    }

    protected virtual void OnStartCleanup(Entity<SurgeryGuideTargetComponent> ent, ref SurgeryGuideStartCleanupMessage args)
    {
        _userInterface.CloseUi(ent.Owner, SurgeryGuideUiKey.Key, args.Actor);
        _popup.PopupPredictedCursor(Loc.GetString("surgery-examine-for-instructions"), args.Actor);
    }

    protected virtual void OnClose(Entity<SurgeryGuideTargetComponent> ent, ref BoundUIClosedEvent args)
    {
        ent.Comp.SelectionOccupied = false;
        Dirty(ent);
    }
}
