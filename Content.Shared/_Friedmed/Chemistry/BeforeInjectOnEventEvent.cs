using Content.Shared.FixedPoint;

namespace Content.Shared._Friedmed.Chemistry;

/// <summary>
/// Event raised before injecting in response to an event, to allow modifying how much is injected
/// </summary>
[ByRefEvent]
public struct BeforeInjectOnEventEvent(FixedPoint2 injectionAmount)
{
    public FixedPoint2 InjectionAmount = injectionAmount;
}
