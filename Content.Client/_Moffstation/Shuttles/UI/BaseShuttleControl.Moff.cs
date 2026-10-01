using System.Numerics;
using Content.Client.Parallax;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public partial class BaseShuttleControl
{
    private const float GridFillTint = 0.35f;

    private static readonly Color ParallaxModulate = Color.White.WithAlpha(0.6f);

    private ParallaxSystem? _parallaxSystem;

    /// <summary>
    /// An opaque grid fill tinted with the grid's colour, so parallax doesn't show through.
    /// </summary>
    protected static Color GetGridFill(Color color)
    {
        return Color.InterpolateBetween(BackingColor, color, GridFillTint);
    }

    /// <summary>
    /// Draws a map's parallax centred on the control, rotated by the view and scrolled as fast as the in-world background.
    /// </summary>
    protected void DrawMapParallax(DrawingHandleScreen handle, MapId mapId, Vector2 worldPosition, Angle viewRotation, float zoom = 1f)
    {
        if (mapId == MapId.Nullspace)
            return;

        _parallaxSystem ??= EntManager.System<ParallaxSystem>();

        var coverRadius = PixelSize.Length / 2f;
        var oldTransform = handle.GetTransform();
        handle.SetTransform(Matrix3Helpers.CreateTransform(PixelSize / 2f, viewRotation) * oldTransform);

        foreach (var layer in _parallaxSystem.GetParallaxLayers(mapId))
        {
            var size = layer.Texture.Size * layer.Config.Scale * UIScale * zoom;

            if (size.X <= 0f || size.Y <= 0f)
                continue;

            var scroll = new Vector2(-worldPosition.X, worldPosition.Y)
                         * EyeManager.PixelsPerMeter * UIScale * zoom * (1f - layer.Config.Slowness);

            if (!layer.Config.Tiled)
            {
                handle.DrawTextureRect(layer.Texture, UIBox2.FromDimensions(scroll - size / 2f, size), ParallaxModulate);
                continue;
            }

            var startX = scroll.X % size.X - MathF.Ceiling((coverRadius + scroll.X % size.X) / size.X) * size.X;
            var startY = scroll.Y % size.Y - MathF.Ceiling((coverRadius + scroll.Y % size.Y) / size.Y) * size.Y;

            for (var x = startX; x < coverRadius; x += size.X)
            {
                for (var y = startY; y < coverRadius; y += size.Y)
                {
                    handle.DrawTextureRect(layer.Texture, UIBox2.FromDimensions(new Vector2(x, y), size), ParallaxModulate);
                }
            }
        }

        handle.SetTransform(oldTransform);
    }
}
