using Content.Shared._Friedmed.Wounds;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.GameStates;

namespace Content.Shared._Friedmed.Organs;

/// <summary>
/// Allows a woundable organ to be targeted to take a share of damage when its parent takes damage.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(WoundableBodySystem), typeof(WoundableOrganSystem))]
public sealed partial class CollateralWoundTargetComponent : Component
{
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// The Min and Max share of damage this organ can take from its parent organ.
    /// </summary>
    [DataField]
    public MinMax DamageShareRange = new(0.05f, 0.5f);
}
