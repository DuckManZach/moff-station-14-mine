using System.Linq;
using Content.Server._Moffstation.Preferences;
using Content.Server._Moffstation.Station;
using Content.Server.Antag.Components;
using Content.Shared.Antag;
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

    // Resolved on demand; a mutual [Dependency] with MoffCharacterPickerSystem would be circular.
    private MoffCharacterPickerSystem MoffCharacterPicker => EntityManager.System<MoffCharacterPickerSystem>();

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
    /// Every antag slot <paramref name="session"/> is currently preselected for, along with the
    /// prototypes a character must have enabled to fill it.
    /// </summary>
    public List<MoffPreSelectedAntag> GetMoffPreSelectedAntags(ICommonSession session)
    {
        var result = new List<MoffPreSelectedAntag>();

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

                // No pref roles means the slot doesn't care which character fills it.
                if (proto.PrefRoles.Count == 0)
                    continue;

                result.Add(new MoffPreSelectedAntag((uid, comp), antag, proto.PrefRoles.ToHashSet()));
            }
        }

        return result;
    }

    /// <summary>
    /// Gives up a preselected slot the character who spawned cannot fill. Losing the antag role is
    /// the tolerable outcome here; the alternative is not spawning the player at all.
    /// </summary>
    public void DropMoffPreSelectedAntag(MoffPreSelectedAntag preSelected, ICommonSession session)
    {
        DeSelectSession(preSelected.GameRule, preSelected.Antag, session);
    }
}

/// <summary>
/// One antag slot a player has been preselected for, and what it takes to fill it. Carries the rule
/// and specifier so the slot can be given up again if no suitable character exists.
/// </summary>
public readonly record struct MoffPreSelectedAntag(
    Entity<AntagSelectionComponent> GameRule,
    ProtoId<AntagSpecifierPrototype> Antag,
    HashSet<ProtoId<AntagPrototype>> PrefRoles);
