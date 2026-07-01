using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Parallax;

[DataDefinition]
public sealed partial class ImageParallaxTextureSource : IParallaxTextureSource
{
    [DataField("path", required: true)]
    public ResPath Path { get; private set; } = default!;
}