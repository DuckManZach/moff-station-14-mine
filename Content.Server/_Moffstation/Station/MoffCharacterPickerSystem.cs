using System.Linq;
using Content.Server.Antag;
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
    [Dependency] private AntagSelectionSystem _antag = default!;
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
    /// Returns and consumes a pending explicit choice, so it applies to exactly one spawn attempt.
    /// </summary>
    /// <remarks>
    /// Does not record the profile as spawned: the attempt can still fail, and a character who never
    /// existed must not turn up later as the one to equip or clone. Callers commit that with
    /// <see cref="RecordSpawnedProfile"/> once the spawn is going ahead.
    /// </remarks>
    public HumanoidCharacterProfile? TakeExplicitChoice(NetUserId player)
    {
        return _explicitChoices.Remove(player, out var profile) ? profile : null;
    }

    /// <summary>
    /// Drops a pending explicit choice, for when the spawn it was pinned for never happened.
    /// </summary>
    public void ClearExplicitChoice(NetUserId player)
    {
        _explicitChoices.Remove(player);
    }

    /// <summary>Records the character a player actually spawned as.</summary>
    public void RecordSpawnedProfile(NetUserId player, HumanoidCharacterProfile profile)
    {
        _spawnedProfiles[player] = profile;
    }

    /// <summary>
    /// Which of <paramref name="player"/>'s active characters spawns as <paramref name="job"/>.
    /// Always returns someone: a readied player spawns.
    /// </summary>
    /// <param name="fallback">
    /// Used when no active character selected <paramref name="job"/> at all, which means the job did
    /// not come from character selection in the first place -- the overflow assignment hands out
    /// Passenger regardless of preference, and admin tooling can name any job. Refusing there would
    /// strand a player upstream always spawns.
    /// </param>
    public HumanoidCharacterProfile PickProfile(
        ICommonSession player,
        ProtoId<JobPrototype> job,
        HumanoidCharacterProfile fallback)
    {
        var eligible = _candidates.GetEligibleProfiles(player.UserId, job);

        if (eligible.Count == 0)
        {
            Log.Debug($"No active character of {player} selected {job}; spawning their lobby character.");
            RecordSpawnedProfile(player.UserId, fallback);
            return fallback;
        }

        // Drop characters that don't meet the job's own requirements, e.g. age or species. This
        // goes through PlayTimeTrackingSystem so that disabled role timers are honored.
        var allowed = eligible.Where(profile => _playTime.IsAllowed(player, job, profile)).ToList();

        if (allowed.Count == 0)
        {
            Log.Warning($"No active character of {player} meets the requirements for {job}; spawning one anyway.");
            allowed = eligible;
        }

        // A preselected antag should be filled by a character that opted in to it. Several rules can
        // preselect the same player, and their antags can live on different characters, so no one
        // character need satisfy them all.
        var preSelected = _antag.GetMoffPreSelectedAntags(player);
        var picked = PickForAntags(allowed, preSelected);

        // Give up the slots the winner can't fill instead of refusing to spawn them. The count was
        // already spent at preselection, so this loses the role exactly like every other way a
        // preselected player fails to take one.
        foreach (var preSelection in preSelected)
        {
            if (preSelection.PrefRoles.Overlaps(picked.AntagPreferences))
                continue;

            Log.Info($"No active character of {player} wants {preSelection.Antag}; giving the slot up.");
            _antag.DropMoffPreSelectedAntag(preSelection, player);
        }

        RecordSpawnedProfile(player.UserId, picked);

        return picked;
    }

    /// <summary>
    /// Picks at random from whichever of <paramref name="allowed"/> satisfies the most preselected
    /// antag slots, so the player keeps as many of them as any one character can.
    /// </summary>
    private HumanoidCharacterProfile PickForAntags(
        List<HumanoidCharacterProfile> allowed,
        List<MoffPreSelectedAntag> preSelected)
    {
        if (preSelected.Count == 0)
            return _random.Pick(allowed);

        var best = new List<HumanoidCharacterProfile>();
        var bestScore = -1;

        foreach (var profile in allowed)
        {
            var score = preSelected.Count(preSelection => preSelection.PrefRoles.Overlaps(profile.AntagPreferences));

            if (score < bestScore)
                continue;

            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
            }

            best.Add(profile);
        }

        return _random.Pick(best);
    }

    /// <summary>For picking a job when the caller has not assigned one, e.g. late joins.</summary>
    public Dictionary<ProtoId<JobPrototype>, JobPriority> GetJobPriorities(
        NetUserId player,
        HumanoidCharacterProfile fallback)
    {
        return _candidates.GetJobPriorities(player, fallback);
    }

    /// <summary>Null if they have not spawned this round.</summary>
    public HumanoidCharacterProfile? GetSpawnedProfile(NetUserId player)
    {
        return _spawnedProfiles.GetValueOrDefault(player);
    }
}
