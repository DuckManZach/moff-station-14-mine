using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Sound;
using Content.Shared._Moffstation.GaussFabricator;
using Content.Shared.Atmos;
using Content.Shared.Database;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.GaussFabricator;

public sealed partial class GaussFabricatorSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AmbientSoundSystem _ambient = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    [Dependency] private EntityQuery<BatteryComponent> _batteryQuery = default!;
    [Dependency] private EntityQuery<PowerNetworkBatteryComponent> _powerBatteryQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Stays here because the attribute can't express ordering.
        UpdatesAfter.Add(typeof(PowerNetSystem));
    }

    [SubscribeLocalEvent]
    private void OnUiOpened(Entity<GaussFabricatorComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnAdjustDrawRate(Entity<GaussFabricatorComponent> ent, ref GaussFabricatorAdjustDrawRateMessage args)
    {
        if (!_powerBatteryQuery.TryComp(ent, out var pnb))
            return;

        if (!float.IsFinite(args.Delta))
            return;

        pnb.MaxChargeRate = Math.Clamp(pnb.MaxChargeRate + args.Delta, ent.Comp.MinDrawRate, ent.Comp.MaxDrawRate);

        _adminLog.Add(LogType.Action, $"{ToPrettyString(args.Actor):actor} set draw rate to {pnb.MaxChargeRate} W on {ToPrettyString(ent):target}");

        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnToggle(Entity<GaussFabricatorComponent> ent, ref GaussFabricatorToggleMessage args)
    {
        if (!_powerBatteryQuery.TryComp(ent, out var pnb))
            return;

        if (pnb.Enabled == args.On)
            return;

        pnb.Enabled = args.On;
        _ambient.SetAmbience(ent.Owner, args.On);
        _adminLog.Add(LogType.Action, $"{ToPrettyString(args.Actor):actor} toggled {ToPrettyString(ent):target} {(args.On ? "on" : "off")}");
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var ent in EntityQueryEnumerator<GaussFabricatorComponent, PowerNetworkBatteryComponent, TransformComponent>())
        {
            var mixture = _atmosphere.GetContainingMixture((ent.Owner, ent.Comp3), excite: ent.Comp2.Enabled);

            ent.Comp2.Efficiency =
                GetBandMultiplier(ent.Comp1, ent.Comp1.TemperatureAcceptable, ent.Comp1.TemperatureOptimal, mixture?.Temperature)
                * GetBandMultiplier(ent.Comp1, ent.Comp1.PressureAcceptable, ent.Comp1.PressureOptimal, mixture?.Pressure);

            if (!ent.Comp2.Enabled)
                continue;

            var received = ent.Comp2.CurrentReceiving;

            // Add waste heat proportional to power draw to the surrounding atmosphere.
            if (received > 0f && mixture != null)
                _atmosphere.AddHeat(mixture, received * ent.Comp1.HeatMultiplier * frameTime);
        }
    }

    /// <summary>
    /// Multiplier for one reading. A missing mixture counts as bad, same as being outside the acceptable range.
    /// </summary>
    private static float GetBandMultiplier(
        GaussFabricatorComponent comp,
        MinMax acceptable,
        MinMax optimal,
        float? value)
    {
        if (value is not { } reading || reading < acceptable.Min || reading > acceptable.Max)
            return comp.BadMultiplier;

        return reading >= optimal.Min && reading <= optimal.Max
            ? comp.OptimalMultiplier
            : comp.NormalMultiplier;
    }

    private void UpdateUi(Entity<GaussFabricatorComponent> ent)
    {
        if (!_powerBatteryQuery.TryComp(ent, out var pnb)
            || !_batteryQuery.TryComp(ent, out var battery))
            return;

        // Null when the fabricator is in space or otherwise not in a gas mixture.
        var mixture = _atmosphere.GetContainingMixture(ent.Owner);

        // One output is spawned per full battery, so outputs per minute is just how fast we fill it.
        var outputRate = battery.MaxCharge > 0f
            ? pnb.CurrentReceiving * pnb.Efficiency * 60f / battery.MaxCharge
            : 0f;

        _uiSystem.SetUiState(
            ent.Owner,
            GaussFabricatorUiKey.Key,
            new GaussFabricatorBuiState(
                pnb.MaxChargeRate,
                pnb.CurrentReceiving,
                ent.Comp.MaxDrawRate,
                _battery.GetChargeLevel((ent.Owner, battery)),
                outputRate,
                new GaussFabricatorGauge(mixture?.Temperature, ent.Comp.TemperatureAcceptable, ent.Comp.TemperatureOptimal),
                new GaussFabricatorGauge(mixture?.Pressure, ent.Comp.PressureAcceptable, ent.Comp.PressureOptimal),
                pnb.Enabled));
    }
}
