using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Zombies;
using Robust.Server.Player;

namespace Content.Server._Harmony.BloodBrothers.EntitySystems;

/// <summary>
/// This handles...
/// </summary>
public sealed class BloodBrotherConvertableSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodBrotherConvertableComponent, MapInitEvent>(OnMapInit);
    }

    private static readonly string RequiredAntagPreference = "BloodBrotherConvertible";

    public void OnMapInit(Entity<BloodBrotherConvertableComponent> ent, ref MapInitEvent args)
    {
        UpdateValid(ent);
    }

    private void UpdateValid(Entity<BloodBrotherConvertableComponent> ent)
    {
        switch (ent.Comp.Preference)
        {
            case false:
                break;
            case null:
                UpdatePreference(ent);
                UpdateConvertible(ent);
                break;
            case true:
                UpdateConvertible(ent);
                break;
        }
    }

    private void UpdatePreference(Entity<BloodBrotherConvertableComponent> ent)
    {
        if (!_player.TryGetSessionByEntity(ent.Owner, out var session))
            return;

        if (!_preferencesManager.TryGetCachedPreferences(session.UserId, out var preferences))
            return;

        var profile = (HumanoidCharacterProfile)preferences.SelectedCharacter;

        if (!profile.AntagPreferences.Contains(RequiredAntagPreference))
        {
            ent.Comp.Preference = false;
        }
    }

    private void UpdateConvertible(Entity<BloodBrotherConvertableComponent> ent)
    {

        ent.Comp.Valid = true;
        return;
        if (!_mind.TryGetMind(ent.Owner, out _, out var targetMind) || targetMind.UserId == null)
        {
            ent.Comp.Valid = false;
        }

        // Target is already a blood brother
        if (HasComp<BloodBrotherComponent>(ent.Owner))
        {
            ent.Comp.Valid = false;
        }

        if (!HasComp<HumanoidAppearanceComponent>(ent.Owner))
        {
            ent.Comp.Valid = false;
        }

        if (HasComp<ZombieComponent>(ent.Owner))
        {
            ent.Comp.Valid = false;
        }

        if (!_mobStateSystem.IsAlive(ent.Owner))
        {
            ent.Comp.Valid = false;
        }

        if (HasComp<MindShieldComponent>(ent.Owner))
        {
            ent.Comp.Valid = false;
        }

        ent.Comp.Valid = true;
    }
}
