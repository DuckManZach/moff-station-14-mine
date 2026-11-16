using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Wounds;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BrainDamageThresholdsSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class BrainDamageThresholdsComponent : Component
{
    /// <summary>
    /// Damage icons to show on medical HUDs when the brain is alive and non-critical.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<StatusIconPrototype>> AliveDamageIcons;

    /// <summary>
    /// Damage icons to show on medical HUDs when the brain is critical.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<StatusIconPrototype>> CriticalDamageIcons;

    /// <summary>
    /// Damage icons to show on medical HUDs when the brain is dead.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StatusIconPrototype> DeadIcon;

    [DataField, AutoNetworkedField]
    public FixedPoint2 DisplayDamage;

    [DataField, AutoNetworkedField]
    public FixedPoint2 DisplayMaxDamage;
}
