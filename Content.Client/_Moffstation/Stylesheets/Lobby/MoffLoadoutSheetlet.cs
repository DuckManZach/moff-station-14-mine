using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Moffstation.Stylesheets.Lobby;

/// <summary>Item cards in the loadout editor.</summary>
[CommonSheetlet]
public sealed class MoffLoadoutSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        StyleBoxFlat Card(string background, string border) => new()
        {
            BackgroundColor = Color.FromHex(background),
            BorderColor = Color.FromHex(border),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 4,
            ContentMarginTopOverride = 4,
            ContentMarginRightOverride = 4,
            ContentMarginBottomOverride = 4,
        };

        return
        [
            E<PanelContainer>().Class(MoffStyleClass.LoadoutCard).Panel(Card("#20262b", "#39444d")),

            // The card's panel is styled through the button so the whole card reacts to hover/press.
            E<ContainerButton>()
                .Class(MoffStyleClass.LoadoutItemCard)
                .PseudoHovered()
                .ParentOf(E<PanelContainer>().Class(MoffStyleClass.LoadoutCard))
                .Panel(Card("#27323a", "#4a5964")),
            E<ContainerButton>()
                .Class(MoffStyleClass.LoadoutItemCard)
                .PseudoPressed()
                .ParentOf(E<PanelContainer>().Class(MoffStyleClass.LoadoutCard))
                .Panel(Card("#304252", "#3e6189")),
            E<ContainerButton>()
                .Class(MoffStyleClass.LoadoutItemCard)
                .PseudoDisabled()
                .ParentOf(E<PanelContainer>().Class(MoffStyleClass.LoadoutCard))
                .Panel(Card("#1b2024", "#30383f")),
        ];
    }
}
