using System.Linq;
using Content.Server._Moffstation.Preferences;
using Content.Server._Moffstation.Station;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Antag;

/// <summary>
/// Multi-character selection support: a player opts in to an antag if any of their active
/// characters wants it, and the character that spawns must be one of those.
/// </summary>
public sealed partial class AntagSelectionSystem
{
    [Dependency] private MoffCharacterSelectionManager _moffCharacterSelection = default!;
    [Dependency] private MoffJobCandidateSystem _moffJobCandidates = default!;

    // Resolved on demand; a mutual [Dependency] with MoffCharacterPickerSystem would be circular.
    private MoffCharacterPickerSystem MoffCharacterPicker => EntityManager.System<MoffCharacterPickerSystem>();

    /// <summary>
    /// Whether some active character of <paramref name="session"/> wants one of
    /// <paramref name="prefRoles"/> alongside every antag they are already pre-selected for. Stops a
    /// combination being picked that no single character can fill, which would otherwise leave the
    /// player in the lobby with neither a job nor an antag.
    /// </summary>
    public bool MoffHasCompatibleCharacter(
        ICommonSession session,
        List<ProtoId<AntagPrototype>> prefs,
        List<ProtoId<AntagPrototype>> prefRoles)
    {
        // Upstream's check first, so bans and playtime still filter the pool.
        if (!PrefsContain(prefs, prefRoles))
            return false;

        // GetMoffPreSelectedAntagPrefRoles skips empty PrefRoles, so nothing constrains these.
        if (prefRoles.Count == 0)
            return true;

        // Pre-selection runs before the player spawns, so ignore any spawned profile here.
        return _moffJobCandidates.GetAntagCompatibleProfiles(session, useSpawnedProfile: false)
            .Any(profile => prefRoles.Any(profile.AntagPreferences.Contains));
    }

    /// <summary>
    /// Every antag preference held by any of the player's active characters, or just the spawned
    /// character's once one has been picked.
    /// </summary>
    public HashSet<ProtoId<AntagPrototype>> GetMoffEnabledAntagPreferences(ICommonSession session)
    {
        var result = new HashSet<ProtoId<AntagPrototype>>();

        // If they've already spawned, get the prefs from the spawned profile
        if (MoffCharacterPicker.GetSpawnedProfile(session.UserId) is { } spawned)
        {
            result.UnionWith(spawned.AntagPreferences);
            return result;
        }

        if (!_pref.TryGetCachedPreferences(session.UserId, out var prefs))
            return result;

        var state = _moffCharacterSelection.GetState(session.UserId);

        foreach (var (slot, profile) in prefs.Characters)
        {
            if (profile == null)
                continue;

            if (!state.IsSlotEnabled(slot))
                continue;

            result.UnionWith(profile.AntagPreferences);
        }

        return result;
    }

    /// <summary>
    /// Per preselected antag, the prototypes a character must have enabled to fill that slot.
    /// </summary>
    public List<HashSet<ProtoId<AntagPrototype>>> GetMoffPreSelectedAntagPrefRoles(ICommonSession session)
    {
        var result = new List<HashSet<ProtoId<AntagPrototype>>>();

        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (HasComp<EndedGameRuleComponent>(uid))
                continue;

            foreach (var antag in comp.Antags)
            {
                if (!comp.PreSelectedSessions.TryGetValue(antag, out var set) || !set.Contains(session))
                    continue;

                if (!ProtoMan.Resolve(antag.Proto, out var proto))
                    continue;

                if (proto.PrefRoles.Count == 0)
                    continue;

                result.Add(proto.PrefRoles.ToHashSet());
            }
        }

        return result;
    }
}
