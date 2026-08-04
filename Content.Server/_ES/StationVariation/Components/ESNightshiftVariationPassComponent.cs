using Content.Server._ES.StationVariation.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._ES.StationVariation.Components;

/// <seealso cref="ESNightshiftVariationPassSystem"/>
[RegisterComponent]
public sealed partial class ESNightshiftVariationPassComponent : Component
{
    /// <summary>
    /// Maps a light's roundstart lamp prototype to the nightshift lamp replacing it.
    /// </summary>
    /// <remarks>
    /// Lamps absent from this map are left alone, so coloured, broken and aged lights keep whatever they had.
    /// </remarks>
    [DataField]
    public Dictionary<EntProtoId, EntProtoId> LampReplacements = new();
}