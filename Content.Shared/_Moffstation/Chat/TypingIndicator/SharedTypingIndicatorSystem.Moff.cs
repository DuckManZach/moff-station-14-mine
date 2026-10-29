using Content.Shared._Moffstation.Chat.TypingIndicator;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Chat.TypingIndicator;

public abstract partial class SharedTypingIndicatorSystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    [Dependency] private EntityQuery<TypingIndicatorComponent> _typingIndicatorQuery;

    [SubscribeLocalEvent]
    private void OnUiTypingIndicatorOpened(Entity<UiTypingIndicatorComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_timing.ApplyingState ||
            !_config.GetCVar(CCVars.ChatShowTypingIndicator) ||
            !_typingIndicatorQuery.TryComp(args.Actor, out var indicator))
            return;

        indicator.TypingIndicatorOverride = ent.Comp.Indicator;
        Dirty(args.Actor, indicator);
        SetTypingIndicatorState(args.Actor, TypingIndicatorState.Typing);
    }

    [SubscribeLocalEvent]
    private void OnUiTypingIndicatorClosed(Entity<UiTypingIndicatorComponent> ent, ref BoundUIClosedEvent args)
    {
        // Leave it alone if the player has started typing in chat since opening the UI.
        if (_timing.ApplyingState ||
            !_typingIndicatorQuery.TryComp(args.Actor, out var indicator) ||
            indicator.TypingIndicatorOverride != ent.Comp.Indicator)
            return;

        foreach (var (uiEntity, _) in _ui.GetActorUis(args.Actor))
        {
            if (uiEntity == ent.Owner)
                return;
        }

        indicator.TypingIndicatorOverride = null;
        Dirty(args.Actor, indicator);
        SetTypingIndicatorState(args.Actor, TypingIndicatorState.None);
    }
}
