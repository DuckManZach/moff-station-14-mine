using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.CharacterSelection;

/// <summary>
/// Everything multi-character selection needs to know about one player, resolved once.
/// Built by <see cref="MoffCharacterRosterSystem.Build(NetUserId)"/>; never construct one by hand.
/// </summary>
public readonly record struct MoffPlayerRoster
{
    public required NetUserId Player { get; init; }

    /// <summary>Every character of theirs whose slot is enabled.</summary>
    public required List<HumanoidCharacterProfile> Active { get; init; }

    /// <summary>
    /// <see cref="Active"/> narrowed to the characters that can fill every antag the player is
    /// pre-selected for. Null when they hold no pre-selection, which is what lets a caller tell
    /// "nothing to narrow against" apart from "narrowed to nothing".
    /// </summary>
    public required List<HumanoidCharacterProfile>? AntagCompatible { get; init; }

    /// <summary>The character they actually spawned as, once one has been committed.</summary>
    public required HumanoidCharacterProfile? Committed { get; init; }

    /// <summary>
    /// The jobs <see cref="Candidates"/> will take, priced from the player-global priorities.
    /// Never-priority jobs are absent, so these are exactly the jobs that can be assigned.
    /// </summary>
    public required Dictionary<ProtoId<JobPrototype>, JobPriority> JobPriorities { get; init; }

    /// <summary>
    /// The characters still in the running. Once one has been committed it is the only answer --
    /// nothing else can be holding the job or antag it was picked for.
    /// </summary>
    public List<HumanoidCharacterProfile> Candidates => Committed is { } committed ? [committed] : PreSpawnCandidates;

    /// <summary>
    /// As <see cref="Candidates"/>, but ignoring <see cref="Committed"/>. Anything asking a
    /// pre-spawn question wants this, or a leftover commit would veto it outright.
    /// </summary>
    public List<HumanoidCharacterProfile> PreSpawnCandidates => AntagCompatible ?? Active;

    public IEnumerable<ProtoId<JobPrototype>> JobsOffered => JobPriorities.Keys;

    public JobPriority GetPriority(ProtoId<JobPrototype> job)
    {
        return JobPriorities.GetValueOrDefault(job, JobPriority.Never);
    }

    /// <summary>Every antag any candidate has opted in to.</summary>
    public HashSet<ProtoId<AntagPrototype>> AntagPreferences()
    {
        var result = new HashSet<ProtoId<AntagPrototype>>();

        foreach (var profile in Candidates)
        {
            result.UnionWith(profile.AntagPreferences);
        }

        return result;
    }

    /// <summary>Whether any candidate has opted in to one of <paramref name="antags"/>.</summary>
    public bool WantsAny(IReadOnlyCollection<ProtoId<AntagPrototype>> antags)
    {
        return Candidates.Any(profile => antags.Any(profile.AntagPreferences.Contains));
    }

    /// <summary>The candidates willing to take <paramref name="job"/>.</summary>
    public List<HumanoidCharacterProfile> CharactersFor(ProtoId<JobPrototype> job)
    {
        return Candidates.Where(profile => profile.JobPriorities.ContainsKey(job)).ToList();
    }
}
