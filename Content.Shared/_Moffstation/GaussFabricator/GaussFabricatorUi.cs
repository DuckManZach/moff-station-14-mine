using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.GaussFabricator;

[Serializable, NetSerializable]
public enum GaussFabricatorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GaussFabricatorBuiState(
    float configuredDrawRate,
    float receivedPower,
    float maxChargeRate,
    float progress,
    bool isOn)
    : BoundUserInterfaceState
{
    public readonly float ConfiguredDrawRate = configuredDrawRate;
    public readonly float ReceivedPower = receivedPower;
    public readonly float MaxChargeRate = maxChargeRate;
    public readonly float Progress = progress;
    public readonly bool IsOn = isOn;

    public override bool Equals(object? obj)
    {
        return obj is GaussFabricatorBuiState other
            && ConfiguredDrawRate.Equals(other.ConfiguredDrawRate)
            && ReceivedPower.Equals(other.ReceivedPower)
            && MaxChargeRate.Equals(other.MaxChargeRate)
            && Progress.Equals(other.Progress)
            && IsOn == other.IsOn;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ConfiguredDrawRate, ReceivedPower, MaxChargeRate, Progress, IsOn);
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
