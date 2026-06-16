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
