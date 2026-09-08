using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.GaussFabricator;

[Serializable, NetSerializable]
public enum GaussFabricatorUiKey : byte
{
    Key,
}

/// <summary>
/// One atmospheric readout: the live value (null when the fabricator isn't in any gas mixture), the band
/// outside which it counts as bad, and the nested band inside which it counts as optimal.
/// </summary>
[Serializable, NetSerializable]
public partial record struct GaussFabricatorGauge(float? Current, MinMax Acceptable, MinMax Optimal)
{
    public readonly float? Current = Current;
    public readonly MinMax Acceptable = Acceptable;
    public readonly MinMax Optimal = Optimal;
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
