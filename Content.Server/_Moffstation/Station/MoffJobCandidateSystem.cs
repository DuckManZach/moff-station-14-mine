using System.Linq;
using Content.Server._Moffstation.Preferences;
using Content.Server.Antag;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Events;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Station;

/// <summary>
/// Sources round-start job candidacy from every one of a player's active characters rather than just
/// the selected one. Uses <see cref="StationJobsGetCandidatesEvent"/> in the opposite direction to
/// JobWhitelistSystem and PlayTimeTrackingSystem, which narrow the same list.
/// </summary>
public sealed partial class MoffJobCandidateSystem : EntitySystem
{
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private MoffCharacterSelectionManager _selection = default!;

    // Resolved on demand; both of these [Dependency] us, so fields here would be circular.
    private AntagSelectionSystem Antag => EntityManager.System<AntagSelectionSystem>();
    private MoffCharacterPickerSystem CharacterPicker => EntityManager.System<MoffCharacterPickerSystem>();

    public override void Initialize()
    {
        base.Initialize();

        // Must run before the two systems that narrow the same list, or the jobs added here would
        // skip their playtime and whitelist filtering.
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnGetCandidates,
            before: [typeof(PlayTimeTrackingSystem), typeof(JobWhitelistSystem)]);
    }

    private void OnGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        // Only when nothing is cached do we leave upstream's seed from the selected character alone.
        // An empty result with state loaded means the player deliberately disabled every slot, which
        // must produce no candidates rather than silently falling back to that disabled character.
        if (!_selection.TryGetState(ev.Player, out _))
            return;

        // Narrowed to the characters that can fill whatever antag they were pre-selected for, so a
        // job can never be assigned that leaves no character able to be both.
        var active = _player.TryGetSessionById(ev.Player, out var session)
            ? GetAntagCompatibleProfiles(session)
            : GetActiveProfiles(ev.Player);

        // Replace rather than add to: the selected character contributes nothing if its slot is
        // inactive, and upstream seeded the list from it unconditionally.
        ev.Jobs.Clear();

        foreach (var profile in active)
        {
            foreach (var job in profile.JobPriorities.Keys)
            {
                if (!ev.Jobs.Contains(job))
                    ev.Jobs.Add(job);
            }
        }
    }

    /// <summary>
    /// Every antag-compatible character of <paramref name="player"/> willing to take
    /// <paramref name="job"/>.
    /// </summary>
    public List<HumanoidCharacterProfile> GetEligibleProfiles(ICommonSession player, ProtoId<JobPrototype> job)
    {
        return GetAntagCompatibleProfiles(player)
            .Where(profile => profile.JobPriorities.ContainsKey(job))
            .ToList();
    }

    /// <summary>
    /// The jobs any antag-compatible character of <paramref name="player"/> will take, at the
    /// player-global priority. <paramref name="fallback"/> covers guests, who have no stored priorities.
    /// </summary>
    public Dictionary<ProtoId<JobPrototype>, JobPriority> GetJobPriorities(
        ICommonSession player,
        HumanoidCharacterProfile fallback)
    {
        var result = new Dictionary<ProtoId<JobPrototype>, JobPriority>();

        foreach (var profile in GetAntagCompatibleProfiles(player))
        {
            foreach (var job in profile.JobPriorities.Keys)
            {
                if (result.ContainsKey(job))
                    continue;

                var priority = _selection.GetEffectivePriority(player.UserId, job, fallback);

                if (priority != JobPriority.Never)
                    result.Add(job, priority);
            }
        }

        return result;
    }

    /// <summary>
    /// The active characters that can fill every antag <paramref name="player"/> is pre-selected for,
    /// or just the spawned character once one has been picked. Job candidacy must be drawn from
    /// exactly this set, or a job can be assigned that no antag-capable character wants.
    /// </summary>
    /// <param name="useSpawnedProfile">
    /// Pass false from anything that runs before the player spawns. Pre-selection is a pre-spawn
    /// question, and a leftover spawned profile would otherwise veto it outright.
    /// </param>
    public List<HumanoidCharacterProfile> GetAntagCompatibleProfiles(
        ICommonSession player,
        bool useSpawnedProfile = true)
    {
        var antagSets = Antag.GetMoffPreSelectedAntagPrefRoles(player);

        // No antag to be compatible with, so don't narrow at all. Notably this keeps a player who
        // ghosted and re-joined free to pick any of their characters again.
        if (antagSets.Count == 0)
            return GetActiveProfiles(player.UserId);

        // Once they have spawned, the character holding the antag is the only one that can fill it.
        if (useSpawnedProfile && CharacterPicker.GetSpawnedProfile(player.UserId) is { } spawned)
            return [spawned];

        var profiles = GetActiveProfiles(player.UserId);

        foreach (var antagSet in antagSets)
        {
            profiles = profiles.Where(profile => antagSet.Overlaps(profile.AntagPreferences)).ToList();
        }

        return profiles;
    }

    /// <summary>Every character of <paramref name="player"/> whose slot is active.</summary>
    public List<HumanoidCharacterProfile> GetActiveProfiles(NetUserId player)
    {
        var result = new List<HumanoidCharacterProfile>();

        if (!_prefs.TryGetCachedPreferences(player, out var prefs))
            return result;

        var state = _selection.GetState(player);

        foreach (var (slot, profile) in prefs.Characters)
        {
            if (profile is HumanoidCharacterProfile humanoid && state.IsSlotEnabled(slot))
                result.Add(humanoid);
        }

        return result;
    }
}
