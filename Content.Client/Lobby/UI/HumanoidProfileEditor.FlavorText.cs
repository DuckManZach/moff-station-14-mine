using Content.Shared._CD.Records;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private bool _allowFlavorText;

    private FlavorText.FlavorText? _flavorText;
    private TextEdit? _flavorTextEdit;

    /// <summary>
    /// Refreshes the flavor text editor status.
    /// </summary>
    public void RefreshFlavorText()
    {
        // Moff Start - Flavor text is a section of the Identity tab rather than a tab of its own.
        if (_allowFlavorText)
        {
            FlavorTextSection.Visible = true;

            if (_flavorText != null)
                return;

            _flavorText = new FlavorText.FlavorText();
            FlavorTextContainer.AddChild(_flavorText);
            _flavorTextEdit = _flavorText.CFlavorTextInput;

            _flavorText.OnFlavorTextChanged += OnFlavorTextChange;

            UpdateFlavorTextEdit();
        }
        else
        {
            FlavorTextSection.Visible = false;

            if (_flavorText == null)
                return;

            FlavorTextContainer.RemoveChild(_flavorText);
            _flavorText.OnFlavorTextChanged -= OnFlavorTextChange;
            _flavorText.Dispose();
            _flavorTextEdit?.Dispose();
            _flavorTextEdit = null;
            _flavorText = null;
        }
        // Moff end
    }

    // CD: Records editor
    private void UpdateProfileRecords(PlayerProvidedCharacterRecords records)
    {
        if (Profile is null)
            return;
        Profile = Profile.WithCDCharacterRecords(records);
        IsDirty = true;
    }

    private void OnFlavorTextChange(string content)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithFlavorText(content);
        SetDirty();
    }

    private void UpdateFlavorTextEdit()
    {
        if (_flavorTextEdit != null)
        {
            _flavorTextEdit.TextRope = new Rope.Leaf(Profile?.FlavorText ?? "");
        }
    }
}
