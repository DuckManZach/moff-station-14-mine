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
    /// Active characters filtered to the characters that can take every antag the player is pre-selected for.
    /// </summary>
    public required List<HumanoidCharacterProfile>? AntagCompatible { get; init; }

    /// <summary>The character the player has spawned as</summary>
    public required HumanoidCharacterProfile? Committed { get; init; }

    /// <summary>
    /// The jobs which can be assigned according to both eligible characters and their priorities
    /// </summary>
    public required Dictionary<ProtoId<JobPrototype>, JobPriority> JobPriorities { get; init; }

    /// <summary>
    /// The characters either eligible to be picked for spawn (with antag selection taken into account), or the one which ended up getting spawned in
    /// </summary>
    public List<HumanoidCharacterProfile> Candidates => Committed is { } committed ? [committed] : PreSpawnCandidates;

    /// <summary>
    /// Candidates, but ignoring whether the player is already spawned
    /// Anything asking a pre-spawn question wants this, in order to not be thrown off by a spawned player.
    /// </summary>
    public List<HumanoidCharacterProfile> PreSpawnCandidates => AntagCompatible ?? Active;


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

    /// <summary>The candidates willing to take <paramref name="job"/>.</summary>
    public List<HumanoidCharacterProfile> CharactersFor(ProtoId<JobPrototype> job)
    {
        return Candidates.Where(profile => profile.JobPriorities.ContainsKey(job)).ToList();
    }
}
