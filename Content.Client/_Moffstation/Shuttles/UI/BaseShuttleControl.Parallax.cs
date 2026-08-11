using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Shuttles.Systems;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public partial class BaseShuttleControl
{
    /// <summary>
    /// Smallest a tile may shrink to on screen. Tile count scales with the inverse square of this, and radars zoom
    /// out far enough that an unclamped 32px texture would want tens of thousands of quads a frame.
    /// </summary>
    private const float MinTilePixels = 64f;

    private IConfigurationManager? _parallaxCfg;
    private ParallaxSystem? _parallaxSystem;
    private ShuttleSystem? _parallaxShuttles;

    /// <summary>
    /// Draws the map's actual parallax behind the radar, layer by layer. <paramref name="viewCentre"/> is where the
    /// view is centred, in world coordinates.
    /// </summary>
    protected void DrawParallaxBackground(DrawingHandleScreen handle, MapId mapId, Vector2 viewCentre)
    {
        _parallaxCfg ??= IoCManager.Resolve<IConfigurationManager>();

        // Players who turned parallax off for performance shouldn't pay for it on the console either.
        if (!_parallaxCfg.GetCVar(CCVars.ParallaxEnabled))
            return;

        _parallaxSystem ??= EntManager.System<ParallaxSystem>();

        var layers = _parallaxSystem.GetParallaxLayers(mapId);

        if (layers.Length == 0)
        {
            // Nothing loaded for this map, so fall back to the console's flat starfield.
            _parallaxShuttles ??= EntManager.System<ShuttleSystem>();

            if (Maps.TryGetMap(mapId, out var mapUid))
                DrawLayer(handle, _parallaxShuttles.GetTexture(mapUid.Value), viewCentre, 1f, Vector2.One, true);

            return;
        }

        foreach (var layer in layers)
        {
            DrawLayer(handle, layer.Texture, viewCentre, layer.Config.Slowness, layer.Config.Scale, layer.Config.Tiled);
        }
    }

    private void DrawLayer(
        DrawingHandleScreen handle,
        Texture tex,
        Vector2 viewCentre,
        float slowness,
        Vector2 scale,
        bool tiled)
    {
        // Slowness 0 pins the layer to the world, 1 pins it to the screen. Same meaning as the in-world overlay.
        var originBL = ScalePosition(viewCentre * (1f - slowness));

        // Clamped, because tile count grows with the inverse square of the tile size as the radar zooms out.
        var size = Vector2.Max(tex.Size * scale * MinimapScale, new Vector2(MinTilePixels));

        originBL -= size / 2f;

        if (!tiled)
        {
            // Planets and nebulae are single features, not a repeating field.
            handle.DrawTextureRect(tex, UIBox2.FromDimensions(originBL, size));
            return;
        }

        // Floor to a whole number of tiles so the seams don't crawl.
        var flooredBL = ((-originBL) / size).Floored() * size + originBL;

        var topRight = PixelSize;

        for (var x = flooredBL.X; x < topRight.X; x += size.X)
        {
            for (var y = flooredBL.Y; y < topRight.Y; y += size.Y)
            {
                handle.DrawTextureRect(tex, new UIBox2(x, y, x + size.X, y + size.Y));
            }
        }
    }
}
