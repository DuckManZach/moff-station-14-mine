using Content.Shared.Shuttles.UI.MapObjects;

namespace Content.Server.Shuttles.Systems;

/// <summary>
/// Exposes the console's exclusion-zone lookup to fork systems. A partial rather than making the upstream member
/// public, so the upstream file keeps a zero diff.
/// </summary>
public sealed partial class ShuttleConsoleSystem
{
    /// <inheritdoc cref="GetExclusions"/>
    public void GetFTLExclusions(ref List<ShuttleExclusionObject>? exclusions)
    {
        GetExclusions(ref exclusions);
    }
}
