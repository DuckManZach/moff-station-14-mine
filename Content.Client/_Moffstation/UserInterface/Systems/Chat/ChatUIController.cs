using Content.Client.CharacterInfo;
using Content.Client.Chat.TypingIndicator;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client._Moffstation.UserInterface.Systems.Chat;


public sealed partial class ChatUIController : SharedTypingIndicatorSystem
{
    [UISystemDependency] private readonly TypingIndicatorSystem? _typingIndicator = default;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public ChatSelectChannel CurrentChannel = ChatSelectChannel.None;
    private static readonly ProtoId<TypingIndicatorPrototype> WhisperProtoId = "whisper";
    private static readonly ProtoId<TypingIndicatorPrototype> EmoteProtoId = "emote";
    private static readonly ProtoId<TypingIndicatorPrototype> OocProtoId = "ooc";
    private static readonly ProtoId<TypingIndicatorPrototype> RadioProtoId = "radio";

    public void NotifySpecificChatTextChange(ChatSelectChannel selectedChannel)
    {
        var channel = CurrentChannel;
        if (CurrentChannel == ChatSelectChannel.None)
            channel = selectedChannel;

        var evt = new BeforeShowTypingIndicatorEvent();
        var overrideIndicator = new ProtoId<TypingIndicatorPrototype>();

        switch (channel)
        {
            case ChatSelectChannel.Whisper:
                overrideIndicator = WhisperProtoId;
                break;

            case ChatSelectChannel.Radio:
                overrideIndicator = RadioProtoId;
                break;

            case ChatSelectChannel.Emotes:
                overrideIndicator = EmoteProtoId;
                break;

            case ChatSelectChannel.LOOC:
            case ChatSelectChannel.OOC:
                overrideIndicator = OocProtoId;
                break;

            default:
                _typingIndicator?.ClientChangedChatText();
                break;
        }

        if (!_prototypeManager.Resolve(overrideIndicator, out var proto))
        {
            Log.Error($"Unknown typing indicator id: {overrideIndicator}");
            return;
        }
    }
}
