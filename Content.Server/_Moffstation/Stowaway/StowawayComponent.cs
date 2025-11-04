namespace Content.Server._Moffstation.Stowaway;

[RegisterComponent]
public sealed partial class StowawayComponent : Component
{
    [DataField]
    public int Players = 1;

    /// <summary>
    /// Max amount of attempts to throw someone in a crate before giving up
    /// </summary>
    [ViewVariables]
    public int MaxAttempts = 50;
}
