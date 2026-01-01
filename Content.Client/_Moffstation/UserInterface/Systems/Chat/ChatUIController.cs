using Content.Client.CharacterInfo;
using Content.Client.Chat.TypingIndicator;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Systems.Chat;


public sealed partial class ChatUIController
{

    public ChatSelectChannel CurrentChannel = ChatSelectChannel.None;
    private static readonly Dictionary<ChatSelectChannel, ProtoId<TypingIndicatorPrototype>> ChannelProtoMap =
            new()
            {
                [ChatSelectChannel.Whisper] = "whisper",
                [ChatSelectChannel.Radio]   = "radio",
                [ChatSelectChannel.Emotes]  = "emote",
                [ChatSelectChannel.LOOC]    = "ooc",
                [ChatSelectChannel.OOC]     = "ooc",
            };

    public void NotifyChatTextChange(ChatSelectChannel channel)
    {
        if (_typingIndicator == null)
            return;

        if (ChannelProtoMap.TryGetValue(channel, out var protoId))
        {
            _typingIndicator.ClientAlternateTyping(protoId);
        }
        else
        {
            _typingIndicator.ClientChangedChatText();
        }
    }
}
