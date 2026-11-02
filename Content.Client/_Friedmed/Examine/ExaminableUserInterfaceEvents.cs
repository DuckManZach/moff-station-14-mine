using Robust.Client.UserInterface.Controls;

namespace Content.Client._Friedmed.Examine;

[ByRefEvent]
public readonly record struct ExaminableUserInterfaceRequestEvent(
    EntityUid Examiner,
    EntityUid Examined,
    BoxContainer Container,
    bool IsInDetailsRange);

[ByRefEvent]
public readonly record struct ExaminableUserInterfaceCloseEvent(EntityUid Examiner);
