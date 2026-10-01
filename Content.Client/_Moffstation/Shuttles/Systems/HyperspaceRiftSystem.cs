using Content.Shared._Moffstation.Shuttles.Systems;

namespace Content.Client._Moffstation.Shuttles.Systems;

public sealed partial class HyperspaceRiftSystem : SharedHyperspaceRiftSystem
{
    /// <summary>
    /// The colour rifts are drawn in on shuttle radars.
    /// </summary>
    public static readonly Color RadarColor = Color.FromHex("#9b6bff");
}
