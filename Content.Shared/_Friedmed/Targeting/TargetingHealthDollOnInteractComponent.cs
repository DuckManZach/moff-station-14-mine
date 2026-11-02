using Robust.Shared.GameStates;

namespace Content.Shared._Friedmed.Targeting;

[RegisterComponent, NetworkedComponent]
[Access(typeof(TargetingHealthDollOnInteractSystem))]
public sealed partial class TargetingHealthDollOnInteractComponent : Component;
