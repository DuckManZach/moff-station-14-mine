using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Robust.Client.Graphics;

namespace Content.Client.Overlays;

/// <summary>
/// Adds a health bar overlay.
/// </summary>
public sealed partial class ShowHealthBarsSystem : EquipmentHudSystem<ShowHealthBarsComponent>
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    private EntityHealthBarOverlay _overlay = default!;
    private Content.Client._Friedmed.Overlays.HeartrateOverlay _heartrate = default!; // Friedmed

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShowHealthBarsComponent, AfterAutoHandleStateEvent>(OnHandleState);

        _overlay = new(EntityManager, ProtoMan);
        _heartrate = new(); // Friedmed
    }

    private void OnHandleState(Entity<ShowHealthBarsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ShowHealthBarsComponent> component)
    {
        base.UpdateInternal(component);

        _overlay.DamageContainers.Clear();
        _overlay.StatusIcon = null;

        foreach (var comp in component.Components)
        {
            foreach (var damageContainerId in comp.DamageContainers)
            {
                _overlay.DamageContainers.Add(damageContainerId);
            }

            _overlay.StatusIcon = comp.HealthStatusIcon;
            _heartrate.StatusIcon = comp.HealthStatusIcon; // Friedmed
        }

        if (!_overlayMan.HasOverlay<EntityHealthBarOverlay>())
        {
            _overlayMan.AddOverlay(_overlay);
        }
        // Friedmed Start
        if (!_overlayMan.HasOverlay<Content.Client._Friedmed.Overlays.HeartrateOverlay>())
        {
            _overlayMan.AddOverlay(_heartrate);
        }
        // Friedmed end
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlay.DamageContainers.Clear();
        _overlayMan.RemoveOverlay(_overlay);
        _overlayMan.RemoveOverlay(_heartrate); // Friedmed
    }
}
