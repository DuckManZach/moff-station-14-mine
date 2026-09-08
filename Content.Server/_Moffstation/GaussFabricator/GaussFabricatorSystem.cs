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

namespace Content.Server._Moffstation.GaussFabricator;

public sealed partial class GaussFabricatorSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private AmbientSoundSystem _ambient = default!;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(PowerNetSystem));

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
        if (!TryComp<PowerNetworkBatteryComponent>(ent, out var pnb))
            return;

        if (!float.IsFinite(args.Delta))
            return;

        pnb.MaxChargeRate = Math.Clamp(pnb.MaxChargeRate + args.Delta, ent.Comp.MinDrawRate, ent.Comp.MaxDrawRate);

        _adminLog.Add(LogType.Action, $"{ToPrettyString(args.Actor):actor} set draw rate to {pnb.MaxChargeRate} W on {ToPrettyString(ent):target}");

        UpdateUi(ent);
    }

    private void OnToggle(Entity<GaussFabricatorComponent> ent, ref GaussFabricatorToggleMessage args)
    {
        if (!TryComp<PowerNetworkBatteryComponent>(ent, out var pnb))
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

        var query = EntityQueryEnumerator<GaussFabricatorComponent, PowerNetworkBatteryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fabricator, out var pnb, out var xform))
        {
            // Only excite the tile while running, so an idle fabricator doesn't wake atmos every tick.
            var mixture = _atmosphere.GetContainingMixture((uid, xform), excite: pnb.Enabled);

            // The solver stores frameTime * CurrentReceiving * Efficiency, so this scales how fast we fill.
            pnb.Efficiency = GetBandMultiplier(fabricator, fabricator.TemperatureAcceptable, fabricator.TemperatureOptimal, mixture?.Temperature)
                             * GetBandMultiplier(fabricator, fabricator.PressureAcceptable, fabricator.PressureOptimal, mixture?.Pressure);

            if (_uiSystem.IsUiOpen(uid, GaussFabricatorUiKey.Key))
                UpdateUi((uid, fabricator));

            if (!pnb.Enabled)
                continue;

            var received = pnb.CurrentReceiving;

            // Add waste heat proportional to power draw to the surrounding atmosphere.
            if (received > 0f && mixture is not null)
                _atmosphere.AddHeat(mixture, received * fabricator.HeatMultiplier * frameTime);
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
        if (!TryComp<PowerNetworkBatteryComponent>(ent, out var pnb)
            || !TryComp<BatteryComponent>(ent, out var battery))
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
