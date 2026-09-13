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
/// <remarks>
/// Everything reads through <see cref="Build(ICommonSession)"/>. Nothing else should re-derive the
/// active set, re-apply the antag narrowing, or reach for the committed character directly.
/// </remarks>
public sealed partial class MoffCharacterRosterSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private MoffCharacterSelectionManager _selection = default!;
    [Dependency] private PlayTimeTrackingSystem _playTime = default!;
    [Dependency] private EntityQuery<EndedGameRuleComponent> _endedRuleQuery = default!;

    /// <summary>
    /// The character each player actually spawned as, so antag loadouts, briefings and admin tools
    /// all act on the body that exists rather than whichever slot is selected in the lobby.
    /// </summary>
    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _committed = new();
    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _requested = new();

    public override void Initialize()
    {
        base.Initialize();

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    #region Roster

    /// <summary>Resolves everything multi-character selection needs to know about a player.</summary>
    /// <param name="assumeSelected">
    /// The profile to fall back to when their selection state is not cached -- a guest, a fabricated
    /// user id, or a database load still in flight. See <see cref="GetActiveCharacters"/>.
    /// </param>
    public MoffPlayerRoster Build(ICommonSession player, HumanoidCharacterProfile? assumeSelected = null)
    {
        return Build(player.UserId, player, assumeSelected);
    }

    /// <inheritdoc cref="Build(ICommonSession, HumanoidCharacterProfile?)"/>
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
            JobPriorities = ResolveJobPriorities(state, candidates, active),
        };
    }

    /// <summary>Every character of the player with their slot enabled.</summary>
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
    /// Filters <paramref name="active"/> to every antag that the player has been pre-selected for
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
    /// The jobs <paramref name="candidates"/> will take, priced from the player-global priorities.
    /// </summary>
    private static Dictionary<ProtoId<JobPrototype>, JobPriority> ResolveJobPriorities(
        MoffCharacterSelectionState? state,
        List<HumanoidCharacterProfile> candidates,
        List<HumanoidCharacterProfile> activeCharacters)
    {
        var result = new Dictionary<ProtoId<JobPrototype>, JobPriority>();

        foreach (var profile in candidates)
        {
            foreach (var job in profile.JobPriorities.Keys)
            {
                if (result.ContainsKey(job))
                    continue;

                var priority = state is { HasPlayerPriorities: true } player
                    ? player.GetPriority(job)
                    : BestPerCharacterPriority(job, activeCharacters, profile);

                if (priority != JobPriority.Never)
                    result.Add(job, priority);
            }
        }

        return result;
    }

    /// <summary>The strongest priority any character in play gives <paramref name="job"/>.</summary>
    private static JobPriority BestPerCharacterPriority(
        ProtoId<JobPrototype> job,
        List<HumanoidCharacterProfile> active,
        HumanoidCharacterProfile candidate)
    {
        var best = candidate.JobPriorities.GetValueOrDefault(job, JobPriority.Never);

        foreach (var profile in active)
        {
            var priority = profile.JobPriorities.GetValueOrDefault(job, JobPriority.Never);

            if (priority > best)
                best = priority;
        }

        return best;
    }

    /// <summary>
    /// Per antag <paramref name="session"/> is pre-selected for, the prototypes a character must have
    /// enabled to fill that slot.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="AntagSelectionComponent.PreSelectedSessions"/> directly rather than going
    /// through AntagSelectionSystem, which depends on this system.
    /// </remarks>
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
    /// Whether some character of <paramref name="session"/> can take one of <paramref name="prefRoles"/>
    /// alongside every antag they are already pre-selected for. Stops a combination being picked that
    /// no single character can fill, which would leave the player in the lobby with neither a job nor
    /// an antag.
    /// </summary>
    public bool HasCharacterFor(ICommonSession session, IReadOnlyCollection<ProtoId<AntagPrototype>> prefRoles)
    {
        // PreSpawnCandidates, not Candidates: pre-selection is a pre-spawn question and a leftover
        // commit would veto it outright.
        var roster = Build(session);

        return roster.PreSpawnCandidates.Any(profile => prefRoles.Any(profile.AntagPreferences.Contains));
    }

    /// <summary>
    /// Every antag preference held by any character still in the running. A player opts in to an
    /// antag if any of their active characters wants it, not just whichever one is selected.
    /// </summary>
    public HashSet<ProtoId<AntagPrototype>> GetAntagPreferences(ICommonSession session)
    {
        return Build(session).AntagPreferences();
    }

    #endregion

    #region Committing a character

    /// <summary>
    /// Picks and records the character that will spawn as <paramref name="job"/>. Null only when the
    /// player has no character able to take it at all; the caller is expected to keep them in the
    /// lobby rather than spawn someone arbitrary.
    /// </summary>
    public HumanoidCharacterProfile? CommitCharacterForJob(ICommonSession session, ProtoId<JobPrototype> job)
    {
        var roster = Build(session);

        // In order of preference: a character that asked for this job, then -- only when they are
        // holding an antag we must not drop -- any antag-compatible character at all.
        List<List<HumanoidCharacterProfile>> tiers = [roster.CharactersFor(job)];

        if (roster.AntagCompatible is { Count: > 0 } forced)
            tiers.Add(forced);

        foreach (var tier in tiers)
        {
            if (tier.Count == 0)
                continue;

            // Drop characters that don't meet the job's own requirements, e.g. age or species. This
            // goes through PlayTimeTrackingSystem so that disabled role timers are honored.
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

    /// <summary>
    /// Picks and records the character a rule that builds its own body should use. Null when the
    /// player holds no antag pre-selection to pick against.
    /// </summary>
    public HumanoidCharacterProfile? CommitCharacterForAntag(ICommonSession session)
    {
        // AntagSelectEntityEvent is raised more than once per antag, so keep the first answer.
        if (_committed.TryGetValue(session.UserId, out var already))
            return already;

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

    /// <summary>
    /// Ghosting means they are no longer embodying the committed character, so a ghost role or a late
    /// rejoin must be free to pick a different one.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnGhostAttached(Entity<GhostComponent> ent, ref PlayerAttachedEvent args)
    {
        Forget(args.Player.UserId);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            Forget(args.Session.UserId);
    }

    private void Forget(NetUserId player)
    {
        _committed.Remove(player);
        _requested.Remove(player);
    }

    #endregion
}
