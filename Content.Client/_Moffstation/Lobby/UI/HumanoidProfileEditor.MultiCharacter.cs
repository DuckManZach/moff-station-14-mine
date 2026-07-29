using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

/// <summary>
/// A character only records <i>whether</i> it will take a job. The priority applied to that job is
/// player-global and lives in the job priority editor, so a character's preference is just yes/no.
/// </summary>
public static class MoffJobPreference
{
    /// <summary>The only two priorities a character can express.</summary>
    public static JobPriority ToPriority(bool enabled)
    {
        return enabled ? JobPriority.Medium : JobPriority.Never;
    }

    /// <summary>Any legacy Low/High on a character collapses onto "yes".</summary>
    public static bool IsEnabled(JobPriority priority)
    {
        return priority != JobPriority.Never;
    }
}

public sealed partial class HumanoidProfileEditor
{
    /// <summary>
    /// Superseded by the player-global priority editor, which has a Passenger priority of its own.
    /// Kept visible-false rather than removed so the setting and its DB column still load.
    /// </summary>
    private void HideMoffPreferenceUnavailable()
    {
        PreferenceUnavailableButton.Visible = false;
    }
}
