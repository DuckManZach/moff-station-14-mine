using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.GaussFabricator;

[Serializable, NetSerializable]
public enum GaussFabricatorUiKey : byte
{
    Key,
}

/// <summary>
/// An outer "acceptable" band with a nested inner "optimal" band, used to draw the atmospheric gauges.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial record struct GaussFabricatorRange(
    float MinAcceptable,
    float MinOptimal,
    float MaxOptimal,
    float MaxAcceptable)
{
    [DataField]
    public float MinAcceptable = MinAcceptable;

    [DataField]
    public float MinOptimal = MinOptimal;

    [DataField]
    public float MaxOptimal = MaxOptimal;

    [DataField]
    public float MaxAcceptable = MaxAcceptable;

    public bool Equals(GaussFabricatorRange other)
    {
        return MinAcceptable.Equals(other.MinAcceptable)
            && MinOptimal.Equals(other.MinOptimal)
            && MaxOptimal.Equals(other.MaxOptimal)
            && MaxAcceptable.Equals(other.MaxAcceptable);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MinAcceptable, MinOptimal, MaxOptimal, MaxAcceptable);
    }
}

/// <summary>
/// One atmospheric readout: the live value, or null when the fabricator isn't in any gas mixture.
/// </summary>
[Serializable, NetSerializable]
public sealed class GaussFabricatorGauge(float? current, GaussFabricatorRange range)
{
    public readonly float? Current = current;
    public readonly GaussFabricatorRange Range = range;

    public override bool Equals(object? obj)
    {
        return obj is GaussFabricatorGauge other
            && Nullable.Equals(Current, other.Current)
            && Range.Equals(other.Range);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Current, Range);
    }
}

[Serializable, NetSerializable]
public sealed class GaussFabricatorBuiState(
    float configuredDrawRate,
    float receivedPower,
    float maxChargeRate,
    float progress,
    float outputRate,
    GaussFabricatorGauge temperature,
    GaussFabricatorGauge pressure,
    bool isOn)
    : BoundUserInterfaceState
{
    public readonly float ConfiguredDrawRate = configuredDrawRate;
    public readonly float ReceivedPower = receivedPower;
    public readonly float MaxChargeRate = maxChargeRate;
    public readonly float Progress = progress;

    /// <summary>
    /// Finished outputs produced per minute at the current charge rate.
    /// </summary>
    public readonly float OutputRate = outputRate;

    public readonly GaussFabricatorGauge Temperature = temperature;
    public readonly GaussFabricatorGauge Pressure = pressure;
    public readonly bool IsOn = isOn;

    public override bool Equals(object? obj)
    {
        return obj is GaussFabricatorBuiState other
            && ConfiguredDrawRate.Equals(other.ConfiguredDrawRate)
            && ReceivedPower.Equals(other.ReceivedPower)
            && MaxChargeRate.Equals(other.MaxChargeRate)
            && Progress.Equals(other.Progress)
            && OutputRate.Equals(other.OutputRate)
            && Temperature.Equals(other.Temperature)
            && Pressure.Equals(other.Pressure)
            && IsOn == other.IsOn;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            ConfiguredDrawRate,
            ReceivedPower,
            MaxChargeRate,
            Progress,
            OutputRate,
            Temperature,
            Pressure,
            IsOn);
    }
}

[Serializable, NetSerializable]
public sealed class GaussFabricatorAdjustDrawRateMessage(float delta) : BoundUserInterfaceMessage
{
    public readonly float Delta = delta;
}

[Serializable, NetSerializable]
public sealed class GaussFabricatorToggleMessage(bool on) : BoundUserInterfaceMessage
{
    public readonly bool On = on;
}