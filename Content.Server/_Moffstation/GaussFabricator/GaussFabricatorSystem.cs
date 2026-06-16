using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Sound;
using Content.Shared._Moffstation.GaussFabricator;
using Content.Shared.Database;
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
    [Dependency] private EmitSoundSystem _sound = default!;

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
        _sound.SetEnabled(ent.Owner, args.On);
        _adminLog.Add(LogType.Action, $"{ToPrettyString(args.Actor):actor} toggled {ToPrettyString(ent):target} {(args.On ? "on" : "off")}");
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GaussFabricatorComponent, PowerNetworkBatteryComponent, BatteryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fabricator, out var pnb, out var battery, out var xform))
        {
            if (_uiSystem.IsUiOpen(uid, GaussFabricatorUiKey.Key))
                UpdateUi((uid, fabricator));

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
        }
    }

    private void UpdateUi(Entity<GaussFabricatorComponent> ent)
    {
        if (!TryComp<PowerNetworkBatteryComponent>(ent, out var pnb)
            || !TryComp<BatteryComponent>(ent, out var battery))
            return;

        _uiSystem.SetUiState(
            ent.Owner,
            GaussFabricatorUiKey.Key,
            new GaussFabricatorBuiState(
                pnb.MaxChargeRate,
                pnb.CurrentReceiving,
                ent.Comp.MaxDrawRate,
                _battery.GetChargeLevel((ent.Owner, battery)),
                pnb.Enabled));
    }
}
