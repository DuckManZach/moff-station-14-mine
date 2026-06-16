using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.Power.Components;
using Content.Shared._Moffstation.GaussFabricator;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Moffstation.GaussFabricator;

public sealed partial class GaussFabricatorSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private AmbientSoundSystem _ambientSound = default!;
    [Dependency] private SharedBatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GaussFabricatorComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
        SubscribeLocalEvent<GaussFabricatorComponent, GaussFabricatorAdjustDrawRateMessage>(OnAdjustDrawRate);
        SubscribeLocalEvent<GaussFabricatorComponent, GaussFabricatorToggleMessage>(OnToggle);
    }

    private void OnUiOpened(Entity<GaussFabricatorComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void OnAdjustDrawRate(Entity<GaussFabricatorComponent> ent, ref GaussFabricatorAdjustDrawRateMessage args)
    {
        if (!TryComp<PowerNetworkBatteryComponent>(ent, out var pnb)
            || !TryComp<BatteryInterfaceComponent>(ent, out var batteryInterface))
            return;

        pnb.MaxChargeRate = Math.Clamp(pnb.MaxChargeRate + args.Delta, batteryInterface.MinChargeRate, batteryInterface.MaxChargeRate);
        UpdateUi(ent);
    }

    private void OnToggle(Entity<GaussFabricatorComponent> ent, ref GaussFabricatorToggleMessage args)
    {
        if (!TryComp<PowerNetworkBatteryComponent>(ent, out var pnb))
            return;

        if (pnb.Enabled == args.On)
            return;

        pnb.Enabled = args.On;
        _ambientSound.SetAmbience(ent.Owner, args.On);
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GaussFabricatorComponent, PowerNetworkBatteryComponent, BatteryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fabricator, out var pnb, out var battery, out var xform))
        {
            if (!pnb.Enabled)
                continue;

            var received = pnb.CurrentReceiving;

            if (battery.MaxCharge > 0f && _battery.GetCharge((uid, battery)) >= battery.MaxCharge)
            {
                _battery.SetCharge((uid, battery), 0f);
                Spawn(fabricator.OutputPrototype, xform.Coordinates);
            }

            // Add waste heat proportional to power draw to the surrounding atmosphere.
            if (received > 0f && _atmosphere.GetContainingMixture((uid, xform), excite: true) is { } mixture)
                _atmosphere.AddHeat(mixture, received * fabricator.HeatMultiplier * frameTime);

            if (_uiSystem.IsUiOpen(uid, GaussFabricatorUiKey.Key))
                UpdateUi((uid, fabricator));
        }
    }

    private void UpdateUi(Entity<GaussFabricatorComponent> ent)
    {
        if (!TryComp<PowerNetworkBatteryComponent>(ent, out var pnb)
            || !TryComp<BatteryComponent>(ent, out var battery)
            || !TryComp<BatteryInterfaceComponent>(ent, out var batteryInterface))
            return;

        _uiSystem.SetUiState(
            ent.Owner,
            GaussFabricatorUiKey.Key,
            new GaussFabricatorBuiState(
                pnb.MaxChargeRate,
                pnb.CurrentReceiving,
                batteryInterface.MaxChargeRate,
                _battery.GetChargeLevel((ent.Owner, battery)),
                pnb.Enabled));
    }
}