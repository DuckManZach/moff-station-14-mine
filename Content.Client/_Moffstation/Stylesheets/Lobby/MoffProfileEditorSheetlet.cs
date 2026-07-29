using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Moffstation.Stylesheets.Lobby;

/// <summary>Surfaces used by the character editor.</summary>
[CommonSheetlet]
public sealed class MoffProfileEditorSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var root = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1b1f23"),
            BorderColor = Color.FromHex("#39424a"),
            BorderThickness = new Thickness(1),
        };

        var card = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#24292e"),
            BorderColor = Color.FromHex("#39424a"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 10,
            ContentMarginTopOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginBottomOverride = 10,
        };

        var sidebar = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#202428e8"),
            BorderColor = Color.FromHex("#39424a"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 8,
            ContentMarginTopOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginBottomOverride = 8,
        };

        var preview = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#202428"),
            BorderColor = Color.FromHex("#39424a"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 10,
            ContentMarginTopOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginBottomOverride = 10,
        };

        var header = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#2b3137"),
            BorderColor = Color.FromHex("#3e6189"),
            BorderThickness = new Thickness(0, 0, 0, 2),
            ContentMarginLeftOverride = 8,
            ContentMarginTopOverride = 5,
            ContentMarginRightOverride = 8,
            ContentMarginBottomOverride = 5,
        };

        return
        [
            E<PanelContainer>().Class(MoffStyleClass.ProfileRoot).Panel(root),
            E<PanelContainer>().Class(MoffStyleClass.ProfileCard).Panel(card),
            E<PanelContainer>().Class(MoffStyleClass.ProfileSidebar).Panel(sidebar),
            E<PanelContainer>().Class(MoffStyleClass.ProfilePreview).Panel(preview),
            E<PanelContainer>().Class(MoffStyleClass.ProfileHeader).Panel(header),

            E<Label>()
                .Class(MoffStyleClass.ProfileTitle)
                .Font(sheet.BaseFont.GetFont(15, FontKind.Bold))
                .FontColor(Color.FromHex("#e3e9ee")),

            // Character rows are large panels, so they get a flatter tint than a regular button.
            E<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Class(MoffStyleClass.CharacterPicker)
                .PseudoNormal()
                .Modulate(Color.FromHex("#343D44")),
            E<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Class(MoffStyleClass.CharacterPicker)
                .PseudoHovered()
                .Modulate(Color.FromHex("#343D44")),
            E<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Class(MoffStyleClass.CharacterPicker)
                .PseudoPressed()
                .Modulate(Color.FromHex("#2A3035")),
            E<ContainerButton>()
                .Class(ContainerButton.StyleClassButton)
                .Class(MoffStyleClass.CharacterPicker)
                .PseudoDisabled()
                .Modulate(Color.FromHex("#202428")),
        ];
    }
}
