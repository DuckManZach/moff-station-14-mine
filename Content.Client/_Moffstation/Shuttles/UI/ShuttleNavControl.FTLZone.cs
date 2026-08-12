using System.Numerics;
using Content.Shared._Moffstation.Shuttles.Systems;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Draws the sector's FTL zone on the pilot's nav radar. Lives in a partial so the upstream control only needs a
/// single call added to its Draw.
/// </summary>
public sealed partial class ShuttleNavControl
{
    private SharedFTLZoneSystem? _zones;

    /// <summary>
    /// Starfield behind the nav radar, matching the sector map. Resolves the map itself so the call can sit right
    /// after DrawBacking, before the range rings get drawn over it.
    /// </summary>
    private void DrawNavParallax(DrawingHandleScreen handle)
    {
        if (_coordinates is not { } coords ||
            _rotation is not { } rotation ||
            !EntManager.TryGetComponent(coords.EntityId, out TransformComponent? xform))
        {
            return;
        }

        // The same angle the grids are drawn at, recomputed here rather than passed in so the upstream Draw only
        // needs the one call. See the ourEntRot/rot pair in ShuttleNavControl.Draw.
        var ourEntRot = RotateWithEntity ? _transform.GetWorldRotation(xform) : rotation;

        DrawParallaxBackground(handle,
            xform.MapID,
            _transform.ToMapCoordinates(coords).Position,
            ourEntRot + rotation);
    }

    /// <summary>
    /// Draws the zone circle, plus an edge-pinned marker when it's off screen so the pilot knows which way to fly.
    /// </summary>
    private void DrawFTLZone(DrawingHandleScreen handle, Matrix3x2 worldToView, EntityUid? mapUid, Vector2 shuttlePos)
    {
        if (mapUid is not { } map)
            return;

        // Resolved lazily because the constructor is upstream.
        _zones ??= EntManager.System<SharedFTLZoneSystem>();

        if (!_zones.TryGetZone(map, out var zone))
            return;

        var worldPos = _transform.GetWorldPosition(zone);
        var viewPos = Vector2.Transform(worldPos, worldToView);

        handle.DrawCircle(viewPos, zone.Comp.Radius * MinimapScale, zone.Comp.Color.WithAlpha(0.05f));
        handle.DrawCircle(viewPos, zone.Comp.Radius * MinimapScale, zone.Comp.Color, filled: false);

        // Normalised rather than clamped, so the marker keeps pointing the right way instead of hugging a corner.
        var offset = viewPos / PixelSize - new Vector2(0.5f, 0.5f);
        var offsetMax = Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) * 2f;

        if (offsetMax <= 1f)
            return;

        var markerPos = (offset / offsetMax + new Vector2(0.5f, 0.5f)) * PixelSize;

        var label = Loc.GetString("shuttle-console-ftl-zone-marker",
            ("distance", $"{(worldPos - shuttlePos).Length():0}"));
        var dimensions = handle.GetDimensions(Font, label, 1f);

        handle.DrawCircle(markerPos, 4f, zone.Comp.Color);
        handle.DrawString(Font,
            Vector2.Clamp(markerPos - new Vector2(dimensions.X / 2f, 0f), Vector2.Zero, PixelSize - dimensions),
            label,
            zone.Comp.Color);
    }
}
