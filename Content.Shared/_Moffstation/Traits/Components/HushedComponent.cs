using Robust.Shared.GameStates;

namespace Content.Shared._Moffstation.Traits.Components;

/// <summary>
/// Entities with this component can only speak in whispers.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HushedComponent : Component;
