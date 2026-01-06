using Content.Client.Mind;
using Content.Shared._Harmony.BloodBrothers.Components;
using Content.Shared._Harmony.BloodBrothers.EntitySystems;
using Content.Shared.Antag;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Harmony.BloodBrothers.EntitySystems;

public sealed class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherComponent, GetStatusIconsEvent>(OnBloodBrotherGetIcons);
        SubscribeLocalEvent<BloodBrotherConvertableComponent, GetStatusIconsEvent>(OnBloodBrotherConvertableGetIcons);
    }

    private void OnBloodBrotherConvertableGetIcons(Entity<BloodBrotherConvertableComponent> entity, ref GetStatusIconsEvent args)
    {
        if (_playerManager.LocalSession?.AttachedEntity is not { } playerEntity)
            return;

        if (!HasComp<BloodBrotherComponent>(playerEntity))
        {
            if (_prototypeManager.TryIndex(entity.Comp.BloodBrotherConvertableIcon, out var iconPrototype))
                args.StatusIcons.Add(iconPrototype);
        }
    }

    private void OnBloodBrotherGetIcons(Entity<BloodBrotherComponent> entity, ref GetStatusIconsEvent args)
    {
        if (_playerManager.LocalSession?.AttachedEntity is not { } playerEntity)
            return;

        if (_mind.TryGetMind(playerEntity, out var mind, out var targetMind) &&
            HasComp<BloodBrotherConvertableComponent>(mind) &&
            entity.Comp.Brother == null)
        {
            if (_prototypeManager.TryIndex(entity.Comp.BloodBrotherConvertableIcon, out var iconPrototype))
                args.StatusIcons.Add(iconPrototype);
        }

        if (HasComp<ShowAntagIconsComponent>(playerEntity) ||
            entity.Owner == playerEntity ||
            entity.Comp.Brother == playerEntity)
        {
            if (_prototypeManager.TryIndex(entity.Comp.BloodBrotherIcon, out var iconPrototype))
                args.StatusIcons.Add(iconPrototype);
        }
    }
}
