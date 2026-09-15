using System.Linq;
using Content.Server._Moffstation.Preferences;
using Content.Server.Antag.Components;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Shared._Moffstation.Preferences;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Moffstation.CharacterSelection;

/// <summary>
/// The single source of truth for multi-character selection: which of a player's characters are in
/// play, which of those can hold the antags they were pre-selected for, what job priorities apply to
/// them, and which one ends up spawning.
/// </summary>
public sealed partial class MoffCharacterRosterSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private MoffCharacterSelectionManager _selection = default!;
    [Dependency] private PlayTimeTrackingSystem _playTime = default!;

    [Dependency] private EntityQuery<EndedGameRuleComponent> _endedRuleQuery;

    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _committed = new();
    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _requested = new();

    #region Roster

    /// <summary>
    /// Resolves everything multi-character selection needs to know about a player.
    /// </summary>
    public MoffPlayerRoster Build(ICommonSession player, HumanoidCharacterProfile? assumeSelected = null)
    {
        return Build(player.UserId, player, assumeSelected);
    }

    public MoffPlayerRoster Build(NetUserId player, HumanoidCharacterProfile? assumeSelected = null)
    {
        _player.TryGetSessionById(player, out var session);
        return Build(player, session, assumeSelected);
    }

    private MoffPlayerRoster Build(
        NetUserId player,
        ICommonSession? session,
        HumanoidCharacterProfile? assumeSelected)
    {
        _selection.TryGetState(player, out var state);
        var active = GetActiveCharacters(player, state, assumeSelected);

        // List compatible with any antags they've been selected for
        // The same as active if they haven't been selected for any
        var antagCompatible =
            session == null
                ? null
                : NarrowToAntags(session, active);

        // If they are spawned already, get their character
        var committed = _committed.GetValueOrDefault(player);

        var candidates = committed != null ? [committed] : antagCompatible ?? active;

        return new MoffPlayerRoster
        {
            Player = player,
            Active = active,
            AntagCompatible = antagCompatible,
            Committed = committed,
            JobPriorities = ResolveJobPriorities(state, candidates),
        };
    }

    /// <summary>
    /// Every character of the player with their slot enabled.
    /// </summary>
    private List<HumanoidCharacterProfile> GetActiveCharacters(
        NetUserId player,
        MoffCharacterSelectionState? state,
        HumanoidCharacterProfile? assumeSelected)
    {
        if (state is not { } selection || !_prefs.TryGetCachedPreferences(player, out var prefs))
            return assumeSelected is null ? [] : [assumeSelected];

        var result = new List<HumanoidCharacterProfile>();

        foreach (var (slot, profile) in prefs.Characters)
        {
            if (selection.IsSlotEnabled(slot))
                result.Add(profile);
        }

        return result;
    }

    /// <summary>
    /// Filters a list of character profiles to every antag that a session has been selected for
    /// </summary>
    private List<HumanoidCharacterProfile>? NarrowToAntags(
        ICommonSession session,
        List<HumanoidCharacterProfile> active)
    {
        var antagSets = GetPreSelectedAntagPrefRoles(session);

        if (antagSets.Count == 0)
            return null;

        var profiles = active;

        foreach (var antagSet in antagSets)
        {
            profiles = profiles.Where(profile => antagSet.Overlaps(profile.AntagPreferences)).ToList();
        }

        return profiles;
    }

    /// <summary>
    /// The jobs the character candidates will take, alongside their job priority
    /// </summary>
    private static Dictionary<ProtoId<JobPrototype>, JobPriority> ResolveJobPriorities(
        MoffCharacterSelectionState? state,
        List<HumanoidCharacterProfile> candidates)
    {
        var result = new Dictionary<ProtoId<JobPrototype>, JobPriority>();

        foreach (var profile in candidates)
        {
            foreach (var job in profile.JobPriorities.Keys)
            {
                if (result.ContainsKey(job))
                    continue;

                var priority = state?.GetPriority(job) ?? JobPriority.Never;

                if (priority != JobPriority.Never)
                    result.Add(job, priority);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the per-character preferences a session needs to fulfill the antag roles it's pre-selected for.
    /// </summary>
    private List<HashSet<ProtoId<AntagPrototype>>> GetPreSelectedAntagPrefRoles(ICommonSession session)
    {
        var result = new List<HashSet<ProtoId<AntagPrototype>>>();
        var query = EntityQueryEnumerator<AntagSelectionComponent, GameRuleComponent>();

        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (_endedRuleQuery.HasComp(uid))
                continue;

            foreach (var antag in comp.Antags)
            {
                if (!comp.PreSelectedSessions.TryGetValue(antag, out var set) || !set.Contains(session))
                    continue;

                if (!_proto.Resolve(antag.Proto, out var proto) || proto.PrefRoles.Count == 0)
                    continue;

                result.Add(proto.PrefRoles.ToHashSet());
            }
        }

        return result;
    }

    #endregion

    #region Antag queries

    /// <summary>
    /// Whether a session has a character that can take at least on of the prefRoles, alongside every other antag they've already been selected for.
    /// </summary>
    public bool HasCharacterFor(ICommonSession session, IReadOnlyCollection<ProtoId<AntagPrototype>> prefRoles)
    {
        var roster = Build(session);

        return roster.PreSpawnCandidates.Any(profile => prefRoles.Any(profile.AntagPreferences.Contains));
    }

    /// <summary>
    /// Every antag preference held by any character still available.
    /// </summary>
    public HashSet<ProtoId<AntagPrototype>> GetAntagPreferences(ICommonSession session)
    {
        return Build(session).AntagPreferences();
    }

    #endregion

    #region Spawning

    public HumanoidCharacterProfile? CommitCharacterForJob(ICommonSession session, ProtoId<JobPrototype> job)
    {
        var roster = Build(session);

        List<List<HumanoidCharacterProfile>> tiers = [roster.CharactersFor(job)];

        if (roster.AntagCompatible is { Count: > 0 } forced)
            tiers.Add(forced);

        foreach (var tier in tiers)
        {
            if (tier.Count == 0)
                continue;

            var qualified = tier.Where(profile => _playTime.IsAllowed(session, job, profile)).ToList();

            if (qualified.Count == 0)
            {
                Log.Warning($"No character of {session} meets the requirements for {job}; spawning one anyway.");
                qualified = tier;
            }

            return Commit(session.UserId, _random.Pick(qualified));
        }

        return null;
    }

    public HumanoidCharacterProfile? CommitCharacterForAntag(ICommonSession session)
    {
        if (_committed.TryGetValue(session.UserId, out var committed))
            return committed;

        if (Build(session).AntagCompatible is not { Count: > 0 } compatible)
            return null;

        return Commit(session.UserId, _random.Pick(compatible));
    }

    /// <summary>Null if they have not spawned this round.</summary>
    public HumanoidCharacterProfile? TryGetCommittedCharacter(NetUserId player)
    {
        return _committed.GetValueOrDefault(player);
    }

    private HumanoidCharacterProfile Commit(NetUserId player, HumanoidCharacterProfile profile)
    {
        _committed[player] = profile;
        return profile;
    }

    #endregion

    #region Explicitly requested characters

    /// <summary>Records the character a late join asked to spawn as.</summary>
    public void RequestCharacter(NetUserId player, HumanoidCharacterProfile profile)
    {
        _requested[player] = profile;
    }

    /// <summary>
    /// Returns and consumes a pending request, so it applies to exactly one spawn.
    /// </summary>
    public HumanoidCharacterProfile? TakeRequestedCharacter(NetUserId player)
    {
        if (!_requested.Remove(player, out var profile))
            return null;

        return Commit(player, profile);
    }

    #endregion

    #region Cleanup

    [SubscribeLocalEvent]
    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _committed.Clear();
        _requested.Clear();
    }

    #endregion
}
