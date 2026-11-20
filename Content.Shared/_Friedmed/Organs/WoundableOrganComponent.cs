using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared._Friedmed.Wounds;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Friedmed.Organs;

/// <summary>
/// Marks an organ as being able to bear wounds
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(WoundableOrganSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class WoundableOrganComponent : Component;
