using System.Linq;
using Content.Shared._Moffstation.Parallax;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Server._Moffstation.Parallax;

/// <summary>
/// Computes and caches the average pixel color of a parallax's image layers,
/// for use by other systems (e.g. setting ambient light to match the background).
/// </summary>
public sealed class ParallaxColorSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    private readonly Dictionary<string, Robust.Shared.Maths.Color?> _cache = new();

    /// <summary>
    /// Returns the pixel-weighted average color of the <see cref="ImageParallaxTextureSource"/>
    /// layers in the parallax prototype matching <paramref name="parallaxId"/>,
    /// converted to linear light. Results are cached after the first call.
    /// Returns false if the prototype has no image layers or the images can't be read.
    /// </summary>
    public bool TryGetParallaxColor(string parallaxId, out Robust.Shared.Maths.Color color)
    {
        if (!_cache.TryGetValue(parallaxId, out var cached))
        {
            cached = ComputeColor(parallaxId);
            _cache[parallaxId] = cached;
        }

        if (cached is null)
        {
            color = default;
            return false;
        }

        color = cached.Value;
        return true;
    }

    private Robust.Shared.Maths.Color? ComputeColor(string parallaxId)
    {
        if (!_proto.TryIndex<ParallaxPrototype>(parallaxId, out var proto))
            return null;

        var paths = proto.Layers
            .Select(l => l.Texture)
            .OfType<ImageParallaxTextureSource>()
            .Select(t => t.Path)
            .ToList();

        if (paths.Count == 0)
            return null;

        double totalR = 0, totalG = 0, totalB = 0, totalWeight = 0;

        foreach (var path in paths)
        {
            if (!_res.TryContentFileRead(path, out var stream))
            {
                Log.Warning($"ParallaxColorSystem: could not open '{path}'");
                continue;
            }

            using (stream)
            using (var image = Image.Load<Rgba32>(stream))
            {
                image.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (var x = 0; x < row.Length; x++)
                        {
                            var px = row[x];
                            var a = px.A / 255.0;
                            if (a <= 0)
                                continue;
                            totalR += px.R * a;
                            totalG += px.G * a;
                            totalB += px.B * a;
                            totalWeight += a;
                        }
                    }
                });
            }
        }

        if (totalWeight <= 0)
            return null;

        var rf = (float)(totalR / totalWeight / 255.0);
        var gf = (float)(totalG / totalWeight / 255.0);
        var bf = (float)(totalB / totalWeight / 255.0);

        // PNG pixels are sRGB; convert to linear for engine color fields like AmbientLightColor.
        return Color.FromSrgb(new Color(rf, gf, bf, 1f));
    }
}
