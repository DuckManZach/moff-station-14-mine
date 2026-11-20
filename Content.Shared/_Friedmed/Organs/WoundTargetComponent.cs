using Content.Shared._Friedmed.Wounds;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Friedmed.Organs;

/// <summary>
/// Allows a woundable organ to be targeted to take damage.
/// Damage taken from this component can be spread to its children and other organs via <see cref="CollateralWoundTargetComponent"/>
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(WoundableBodySystem), typeof(WoundableOrganSystem))]
public sealed partial class WoundTargetComponent : Component
{
    [DataField]
    public float Weight;

    /// <summary>
    /// The minimum damage threshold for an organ to transfer damage to a child organ
    /// </summary>
    [DataField]
    public FixedPoint2 CollateralThreshold = 10;

    /// <summary>
    /// The chance that damage will be spread to a child organ
    /// </summary>
    [DataField]
    public float CollateralChance = 0.5f;
}

/// <summary>
/// Raised on a body to get a by-weight dictionary of the organs a hit on it can land on.
/// </summary>
[ByRefEvent]
public readonly record struct WoundTargetWeightsEvent(Dictionary<Entity<WoundTargetComponent>, float> Weights);
