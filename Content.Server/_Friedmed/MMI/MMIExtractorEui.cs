using Content.Server.EUI;
using Content.Shared._Friedmed.MMI;
using Content.Shared.DoAfter;
using Content.Shared.Eui;

namespace Content.Server._Friedmed.MMI;

public sealed class MMIExtractorEui : BaseEui
{
    private readonly MMIExtractorSystem _mmiExtractor;
    private readonly DoAfterId _doAfterId;

    public MMIExtractorEui(MMIExtractorSystem mmiExtractor, DoAfterId doAfterId)
    {
        _mmiExtractor = mmiExtractor;
        _doAfterId = doAfterId;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not MMIExtractorMessage choice || !choice.Accepted)
            _mmiExtractor.Decline(_doAfterId);
        else
            _mmiExtractor.Accept(_doAfterId);

        Close();
    }
}
