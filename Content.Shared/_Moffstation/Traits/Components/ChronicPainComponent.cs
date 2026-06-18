namespace Content.Shared._Moffstation.Traits.Components;

[RegisterComponent]
public sealed partial class ChronicPainComponent : Component
{
    [DataField]
    public TimeSpan WithdrawalInterval = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(3);

    public float Timer;
}
