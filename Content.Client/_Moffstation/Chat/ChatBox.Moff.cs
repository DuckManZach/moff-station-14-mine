using Content.Shared._Moffstation.CCVar;
using Content.Shared.Chat;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private readonly List<StackedLine> _stackedLines = new();

    /// Adds a chat line, or merges it into an identical line among the last few and moves that line to the bottom.
    private void AddStackedLine(ChatMessage msg, Color color)
    {
        var lookback = _cfg.GetCVar(MoffCCVars.ChatStackLookback);
        for (var i = _stackedLines.Count - 1; i >= 0; i--)
        {
            if (_stackedLines[i].Index < Contents.EntryCount - lookback)
                _stackedLines.RemoveAt(i);
        }

        if (lookback <= 0 || (msg.Channel & ChatChannel.AdminRelated) != 0)
        {
            AddLine(msg.WrappedMessage, color);
            return;
        }

        foreach (var line in _stackedLines)
        {
            if (line.Message != msg.WrappedMessage || line.Color != color)
                continue;

            line.Count++;
            var stacked = new FormattedMessage(line.Base);
            stacked.AddMarkupOrThrow(Loc.GetString("chat-box-stack-count", ("count", line.Count)));

            // Removing before adding makes the whole log jump.
            Contents.AddMessage(stacked, tagsAllowed: null);
            Contents.RemoveEntry(line.Index);

            foreach (var other in _stackedLines)
            {
                if (other.Index > line.Index)
                    other.Index--;
            }

            line.Index = Contents.EntryCount - 1;
            return;
        }

        AddLine(msg.WrappedMessage, color);
        _stackedLines.Add(new StackedLine(msg.WrappedMessage, color, Contents.GetMessage(^1), Contents.EntryCount - 1));
    }

    private sealed class StackedLine(string message, Color color, FormattedMessage baseLine, int index)
    {
        public readonly string Message = message;
        public readonly Color Color = color;
        public readonly FormattedMessage Base = baseLine;
        public int Index = index;
        public int Count = 1;
    }
}
