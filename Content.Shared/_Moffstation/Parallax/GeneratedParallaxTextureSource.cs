using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Parallax;

[DataDefinition]
public sealed partial class GeneratedParallaxTextureSource : IParallaxTextureSource
{
    [DataField("configPath")]
    public ResPath ParallaxConfigPath { get; private set; } = new("/parallax_config.toml");

    [DataField("id")]
    public string Identifier { get; private set; } = "other";
}