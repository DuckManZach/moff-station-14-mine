using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Parallax;

[Prototype]
public sealed partial class ParallaxPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("layers")]
    public List<ParallaxLayerConfig> Layers { get; private set; } = new();

    [DataField("layersLQ")]
    public List<ParallaxLayerConfig> LayersLQ { get; private set; } = new();

    /// <summary>
    /// If low-quality layers don't exist for this parallax and high-quality should be used instead.
    /// </summary>
    [DataField("layersLQUseHQ")]
    public bool LayersLQUseHQ { get; private set; } = true;
}