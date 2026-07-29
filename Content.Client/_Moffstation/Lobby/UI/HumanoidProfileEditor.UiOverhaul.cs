using Content.Client._Moffstation.Antags.UI;
using Content.Shared.Preferences;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

/// <summary>
/// Glue between <see cref="HumanoidProfileEditor"/> and the loadout and roles editors that replaced its
/// jobs and loadout tabs.
/// </summary>
public sealed partial class HumanoidProfileEditor
{
    private const int LoadoutTabIndex = 2;

    /// <summary>The antag tab moved inside the Roles tab; forwarded so existing wiring still resolves.</summary>
    private SpecialRolesTab Antags => RolesEditor.Antags;

    /// <summary>Likewise for the preference-unavailable button, which the roles editor now owns.</summary>
    private OptionButton PreferenceUnavailableButton => RolesEditor.PreferenceUnavailableButton;

    private void InitializeMoffEditors()
    {
        TabContainer.SetTabTitle(0, Loc.GetString("humanoid-profile-editor-identity-tab"));
        TabContainer.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-appearance-tab"));
        TabContainer.SetTabTitle(LoadoutTabIndex, Loc.GetString("humanoid-profile-editor-loadout-tab"));
        TabContainer.SetTabTitle(3, Loc.GetString("humanoid-profile-editor-roles-tab"));
        TabContainer.SetTabTitle(4, Loc.GetString("humanoid-profile-editor-traits-tab"));

        LoadoutEditor.ProfileChanged += ApplyMoffProfileChange;
        RolesEditor.ProfileChanged += ApplyMoffProfileChange;

        LoadoutEditor.PreviewJobChanged += job =>
        {
            JobOverride = job;
            ReloadPreview();
        };

        RolesEditor.PreviewJobChanged += job =>
        {
            JobOverride = job;
            ReloadPreview();
        };

        // The Loadout button on a job card opens that job directly in the Loadout tab.
        RolesEditor.LoadoutRequested += job =>
        {
            LoadoutEditor.SyncProfile(Profile);
            LoadoutEditor.SelectJob(job.ID);
            TabContainer.CurrentTab = LoadoutTabIndex;
        };

        RolesEditor.OpenGuidebookRequested += pages => OnOpenGuidebook?.Invoke(pages);

        HideMoffPreferenceUnavailable();
    }

    private void ApplyMoffProfileChange(HumanoidCharacterProfile profile)
    {
        Profile = profile;

        LoadoutEditor.SyncProfile(profile);
        RolesEditor.SyncProfile(profile);

        SetDirty();
        ReloadPreview();
    }
}
