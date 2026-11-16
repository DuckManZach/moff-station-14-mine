using Robust.Shared.GameStates;

namespace Content.Shared._Friedmed.Medical;

/// <summary>
/// Organs with this component can be manually examined.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PalpatableOrganComponent : Component
{
    /// <summary>
    ///     Time it takes to palpate this organ.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);
}

