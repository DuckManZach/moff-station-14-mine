using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Parallax.Managers;
using Content.Client.Shuttles.Systems;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public partial class BaseShuttleControl
{
    /// <summary>
    /// Range at which the parallax is drawn at exactly the size it appears in-game. The geometric mean of the nav
    /// radar's 64m-256m, so that range straddles in-game scale instead of only ever falling short of it.
    /// </summary>
    private const float ParallaxReferenceRange = 128f;

    /// <summary>
    /// Pixels per metre the parallax is drawn at. Deliberately not <see cref="MapGridControl.MinimapScale"/>, which
    /// is ~5 where the game draws the world at 32 and so shrinks the backdrop into noise. Anchored to the game's own
    /// scale at <see cref="ParallaxReferenceRange"/> and taken by root of the zoom from there, so it still responds
    /// to zoom without running away at either end.
    /// </summary>
    private float ParallaxScale =>
        EyeManager.PixelsPerMeter * UIScale * MathF.Sqrt(ParallaxReferenceRange / WorldRange);

    /// <summary>
    /// Shortest side a tile may shrink to before the texture is doubled up to stay above it. Only pathological
    /// parallaxes with tiny textures reach this, since <see cref="ParallaxScale"/> barely varies with zoom.
    /// </summary>
    private const float MinTilePixels = 8f;

    private IConfigurationManager? _parallaxCfg;
    private IParallaxManager? _parallaxManager;
    private ParallaxSystem? _parallaxSystem;
    private ShuttleSystem? _parallaxShuttles;

    /// <summary>
    /// Draws the map's actual parallax behind the radar, layer by layer, using the same maths as
    /// <see cref="ParallaxOverlay"/> so a layer drifts across the radar at the speed it drifts in-world.
    /// <paramref name="viewCentre"/> is where the view is centred in world coordinates, and
    /// <paramref name="rotation"/> is the angle the radar's world axes are drawn at.
    /// </summary>
    protected void DrawParallaxBackground(
        DrawingHandleScreen handle,
        MapId mapId,
        Vector2 viewCentre,
        Angle rotation = default)
    {
        _parallaxCfg ??= IoCManager.Resolve<IConfigurationManager>();

        // Players who turned parallax off for performance shouldn't pay for it on the console either.
        if (!_parallaxCfg.GetCVar(CCVars.ParallaxEnabled))
            return;

        _parallaxSystem ??= EntManager.System<ParallaxSystem>();
        _parallaxManager ??= IoCManager.Resolve<IParallaxManager>();

        // Rotating the lattice rather than the maths keeps tiling axis-aligned, so the starfield turns under a radar
        // that spins with the shuttle instead of staying pinned to the screen.
        var oldTransform = handle.GetTransform();
        var rotated = rotation != Angle.Zero;

        if (rotated)
            handle.SetTransform(Matrix3x2.CreateRotation((float) rotation.Theta, MidPointVector) * oldTransform);

        // Rotation sweeps the corners through the disc around the midpoint, so tile that instead of the control box.
        Vector2 pixelSize = PixelSize;
        var bounds = rotated
            ? new UIBox2(MidPointVector - RotatedExtent, MidPointVector + RotatedExtent)
            : new UIBox2(Vector2.Zero, pixelSize);

        var layers = _parallaxSystem.GetParallaxLayers(mapId);

        if (layers.Length == 0)
        {
            // Nothing loaded for this map yet, so fall back to the console's flat starfield at the layer defaults.
            _parallaxShuttles ??= EntManager.System<ShuttleSystem>();

            if (Maps.TryGetMap(mapId, out var mapUid))
            {
                var texture = _parallaxShuttles.GetTexture(mapUid.Value);
                DrawLayer(handle, texture, bounds, viewCentre, viewCentre * 0.5f, Vector2.One, true);
            }
        }
        else
        {
            var realTime = (float) Timing.RealTime.TotalSeconds;

            foreach (var layer in layers)
            {
                var cfg = layer.Config;

                // Unclamped lerp between the layer's home and the view centre: slowness 0 pins it to the world,
                // 1 pins it to the view. Identical to ParallaxOverlay, minus the shader.
                var home = cfg.WorldHomePosition + _parallaxManager.ParallaxAnchor;
                var centre = (viewCentre - home) * cfg.Slowness
                             + home
                             + cfg.WorldAdjustPosition
                             + cfg.Scrolling * realTime;

                DrawLayer(handle, layer.Texture, bounds, viewCentre, centre, cfg.Scale, cfg.Tiled);
            }
        }

        if (rotated)
            handle.SetTransform(oldTransform);
    }

    /// <summary>
    /// Half-width of the square that covers everything the control's corners sweep through when the lattice rotates
    /// about <see cref="MapGridControl.MidPointVector"/>.
    /// </summary>
    private Vector2 RotatedExtent
    {
        get
        {
            Vector2 pixelSize = PixelSize;
            var radius = Math.Max(MidPointVector.Length(), (pixelSize - MidPointVector).Length());
            return new Vector2(radius);
        }
    }

    /// <summary>
    /// <paramref name="centre"/> is where the layer's texture is centred, in world coordinates.
    /// </summary>
    private void DrawLayer(
        DrawingHandleScreen handle,
        Texture tex,
        UIBox2 bounds,
        Vector2 viewCentre,
        Vector2 centre,
        Vector2 scale,
        bool tiled)
    {
        var parallaxScale = ParallaxScale;

        // Texture size in world units, exactly as in-world, then back to pixels at the parallax's own scale.
        var worldSize = tex.Size / (float) EyeManager.PixelsPerMeter * scale;
        var size = worldSize * parallaxScale;

        if (tiled)
        {
            // Doubling up to the floor rather than clamping to it keeps the texture's aspect ratio.
            var shortest = MathF.Min(size.X, size.Y);

            if (shortest < MinTilePixels)
                size *= MathF.Pow(2f, MathF.Ceiling(MathF.Log2(MinTilePixels / shortest)));
        }

        // A zero-sized texture would never advance the tiling loop.
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0f || size.Y <= 0f)
            return;

        // World Y is up, screen Y is down.
        var offset = centre - viewCentre;
        var originTL = MidPointVector + new Vector2(offset.X, -offset.Y) * parallaxScale - size / 2f;

        if (!tiled)
        {
            // Planets and nebulae are single features, not a repeating field.
            handle.DrawTextureRect(tex, UIBox2.FromDimensions(originTL, size));
            return;
        }

        // Floor to a whole number of tiles from the origin so the seams don't crawl.
        var flooredTL = ((bounds.TopLeft - originTL) / size).Floored() * size + originTL;

        for (var x = flooredTL.X; x < bounds.Right; x += size.X)
        {
            for (var y = flooredTL.Y; y < bounds.Bottom; y += size.Y)
            {
                handle.DrawTextureRect(tex, new UIBox2(x, y, x + size.X, y + size.Y));
            }
        }
    }
}