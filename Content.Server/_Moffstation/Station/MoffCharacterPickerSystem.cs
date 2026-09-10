using System.Linq;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.Station;

/// <summary>
/// Picks which of a player's active characters spawns, once a job has been assigned to them.
/// </summary>
public sealed partial class MoffCharacterPickerSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MoffJobCandidateSystem _candidates = default!;
    [Dependency] private PlayTimeTrackingSystem _playTime = default!;

    /// <summary>
    /// So antag loadouts equip the character that spawned, not the one selected in the lobby.
    /// </summary>
    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _spawnedProfiles = new();

    /// <summary>
    /// A late join names the character it wants, so nothing should pick one at random for it.
    /// </summary>
    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _explicitChoices = new();

    [SubscribeLocalEvent]
    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _spawnedProfiles.Clear();
        _explicitChoices.Clear();
    }

    /// <summary>Records the character a late join asked to spawn as.</summary>
    public void SetExplicitChoice(NetUserId player, HumanoidCharacterProfile profile)
    {
        _explicitChoices[player] = profile;
    }

    /// <summary>
    /// Returns and consumes a pending explicit choice, so it applies to exactly one spawn.
    /// </summary>
    public HumanoidCharacterProfile? TakeExplicitChoice(NetUserId player)
    {
        if (!_explicitChoices.Remove(player, out var profile))
            return null;

        _spawnedProfiles[player] = profile;
        return profile;
    }

    /// <summary>
    /// Null only when the player has no active character at all willing to take
    /// <paramref name="job"/>; the caller is expected to fall back rather than drop the player.
    /// </summary>
    public HumanoidCharacterProfile? PickProfile(ICommonSession player, ProtoId<JobPrototype> job)
    {
        // Null means they are not a pre-selected antag, so nothing narrows their characters.
        var antagCompatible = _candidates.GetAntagCompatibleProfiles(player);
        var candidates = antagCompatible ?? _candidates.GetActiveProfiles(player.UserId);

        var eligible = candidates.Where(profile => profile.JobPriorities.ContainsKey(job)).ToList();

        if (eligible.Count == 0)
        {
            // A pre-selected antag can be forced onto the overflow job no character enabled, rather
            // than being dropped. Anyone else who wants none of their jobs stays in the lobby.
            if (antagCompatible is not { Count: > 0 })
                return null;

            eligible = antagCompatible;
        }

        // Drop characters that don't meet the job's own requirements, e.g. age or species. This
        // goes through PlayTimeTrackingSystem so that disabled role timers are honored.
        var allowed = eligible.Where(profile => _playTime.IsAllowed(player, job, profile)).ToList();

        if (allowed.Count == 0)
        {
            Log.Warning($"No active character of {player} meets the requirements for {job}; spawning one anyway.");
            allowed = eligible;
        }

        var picked = _random.Pick(allowed);
        _spawnedProfiles[player.UserId] = picked;

        return picked;
    }

    public HumanoidCharacterProfile? PickAntagProfile(ICommonSession player)
    {
        // AntagSelectEntityEvent is raised more than once per antag, so keep the first answer.
        if (_spawnedProfiles.TryGetValue(player.UserId, out var already))
            return already;

        var compatible = _candidates.GetAntagCompatibleProfiles(player);

        if (compatible is not { Count: > 0 })
            return null;

        var picked = _random.Pick(compatible);
        _spawnedProfiles[player.UserId] = picked;

        return picked;
    }

    /// <summary>Null if they have not spawned this round.</summary>
    public HumanoidCharacterProfile? GetSpawnedProfile(NetUserId player)
    {
        return _spawnedProfiles.GetValueOrDefault(player);
    }
}
