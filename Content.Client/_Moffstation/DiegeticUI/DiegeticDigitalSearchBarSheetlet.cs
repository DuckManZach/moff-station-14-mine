using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Moffstation.DiegeticUI;

/// <summary>
/// Green-on-black terminal styling for search fields embedded in diegetic machine UIs.
/// </summary>
[CommonSheetlet]
public sealed class DiegeticDigitalSearchBarSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    private const string StyleClassDiegeticSearchBar = "MoffDiegeticSearchBar";
    

    public override StyleRule[] GetRules(T sheet, object config)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = MoffDiegeticColors.ScreenBackground,
            BorderColor = MoffDiegeticColors.ScreenBorder,
            BorderThickness = new Thickness(2),
        };
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 6);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 4);

        return
        [
            E<LineEdit>()
                .Class(StyleClassDiegeticSearchBar)
                .Prop(LineEdit.StylePropertyStyleBox, box)
                .FontColor(MoffDiegeticColors.ScreenText)
                .Prop(LineEdit.StylePropertyCursorColor, MoffDiegeticColors.ScreenText),
            E<LineEdit>()
                .Class(StyleClassDiegeticSearchBar)
                .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .FontColor(MoffDiegeticColors.ScreenTextDim),
        ];
    }
}
