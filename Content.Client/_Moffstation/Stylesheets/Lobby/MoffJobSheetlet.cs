using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Moffstation.Stylesheets.Lobby;

/// <summary>Job cards in the roles editor.</summary>
[CommonSheetlet]
public sealed class MoffJobSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        StyleBoxFlat Box(string background, string border, float margin = 8) => new()
        {
            BackgroundColor = Color.FromHex(background),
            BorderColor = Color.FromHex(border),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = margin,
            ContentMarginTopOverride = margin,
            ContentMarginRightOverride = margin,
            ContentMarginBottomOverride = margin,
        };

        return
        [
            E<PanelContainer>().Class(MoffStyleClass.JobCard).Panel(Box("#20262b", "#39444d")),
            E<PanelContainer>().Class(MoffStyleClass.JobIcon).Panel(Box("#181d21", "#303a42", 3)),
            E<PanelContainer>().Class(MoffStyleClass.JobDescriptionPanel).Panel(Box("#181d21", "#303a42", 6)),

            E<Label>()
                .Class(MoffStyleClass.JobName)
                .Font(sheet.BaseFont.GetFont(15, FontKind.Bold))
                .FontColor(Color.FromHex("#e4e9ed")),

            // The title row is a button only so the card can expand; it should not look like one.
            E<ContainerButton>()
                .Class(MoffStyleClass.JobTitleButton)
                .PseudoNormal()
                .Panel(Box("#00000000", "#00000000", 3)),
            E<ContainerButton>()
                .Class(MoffStyleClass.JobTitleButton)
                .PseudoHovered()
                .Panel(Box("#27323a", "#00000000", 3)),
            E<ContainerButton>()
                .Class(MoffStyleClass.JobTitleButton)
                .PseudoPressed()
                .Panel(Box("#304252", "#00000000", 3)),
        ];
    }
}
