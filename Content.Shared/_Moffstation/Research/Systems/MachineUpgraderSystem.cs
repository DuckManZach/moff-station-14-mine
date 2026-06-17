using Content.Shared._Moffstation.Research.Components;
using Content.Shared.DoAfter;

namespace Content.Shared._Moffstation.Research.Systems;

/// <summary>
/// Shared portion of the RPED system — only handles DoAfter attempt validation so it runs on both sides.
/// All actual logic (research check, cost, upgrade finalization) is in the server system.
/// </summary>
public sealed class MachineUpgraderSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<MachineUpgraderComponent, DoAfterAttemptEvent<RPEDDoAfterEvent>>(OnDoAfterAttempt);
    }

    private void OnDoAfterAttempt(EntityUid uid, MachineUpgraderComponent comp, DoAfterAttemptEvent<RPEDDoAfterEvent> args)
    {
        if (args.Event.Target is not { } target || TerminatingOrDeleted(target))
            args.Cancel();
    }
}