using Content.Client.Chat.TypingIndicator;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.DeltaV.AACTablet;
using Robust.Shared.Prototypes;

namespace Content.Client.DeltaV.AACTablet.UI;

public sealed class AACBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    [ViewVariables]
    private AACWindow? _window;

    private static readonly ProtoId<TypingIndicatorPrototype> AACTypingIndicator = "aac";

    private TypingIndicatorSystem? _typing;

    public AACBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window?.Close();
        _window = new AACWindow(this, _prototypeManager);
        _window.OpenCentered();

        _window.PhraseButtonPressed += OnPhraseButtonPressed;
        _window.OnClose += Close;
    }

    private void OnPhraseButtonPressed(string phraseId)
    {
        SendMessage(new AACTabletSendPhraseMessage(phraseId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Dispose();
    }

    // TODO: Add the AAC Tablet Updates
    // private void OnTyping()
    // {
    //     _typing ??= EntMan.System<TypingIndicatorSystem>();
    //     _typing?.ClientAlternateTyping(AACTypingIndicator);
    // }
    //
    // private void OnSubmit()
    // {
    //     _typing ??= EntMan.System<TypingIndicatorSystem>();
    //     _typing?.ClientSubmittedChatText();
    // }
}
