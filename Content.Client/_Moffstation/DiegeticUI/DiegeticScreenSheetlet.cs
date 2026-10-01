using Content.Client.Resources;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Moffstation.DiegeticUI;

/// <summary>
/// Green-on-black terminal screen panels and text for diegetic machine UIs.
/// </summary>
[CommonSheetlet]
public sealed class DiegeticScreenSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    public const string StyleClassScreen = "MoffDiegeticScreen";
    public const string StyleClassScreenText = "MoffDiegeticScreenText";
    public const string StyleClassScreenTextDim = "MoffDiegeticScreenTextDim";

    private const string TerminalFont = "/Fonts/Sysfont/Sysfont-Regular.otf";

    public override StyleRule[] GetRules(T sheet, object config)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = MoffDiegeticColors.ScreenBackground,
            BorderColor = MoffDiegeticColors.ScreenBorder,
            BorderThickness = new Thickness(2),
        };
        box.SetContentMarginOverride(StyleBox.Margin.All, 6);

        return
        [
            E<PanelContainer>().Class(StyleClassScreen).Panel(box),
            E<Label>()
                .Class(StyleClassScreenText)
                .Font(ResCache.GetFont(TerminalFont, 16))
                .FontColor(MoffDiegeticColors.ScreenText),
            E<Label>()
                .Class(StyleClassScreenTextDim)
                .Font(ResCache.GetFont(TerminalFont, 10))
                .FontColor(MoffDiegeticColors.ScreenTextDim),
        ];
    }
}
