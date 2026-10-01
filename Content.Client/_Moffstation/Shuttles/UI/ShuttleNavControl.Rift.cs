using System.Numerics;
using Content.Client._Moffstation.Shuttles.Systems;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleNavControl
{
    private HyperspaceRiftSystem? _rifts;

    /// <summary>
    /// Draws the map's hyperspace rift, keeping its label on the edge of the radar while it is out of range.
    /// </summary>
    private void DrawRift(DrawingHandleScreen handle, EntityUid? mapUid, Matrix3x2 worldToView)
    {
        _rifts ??= EntManager.System<HyperspaceRiftSystem>();

        if (!_rifts.TryGetRift(mapUid, out var rift))
            return;

        var centre = Vector2.Transform(_transform.GetWorldPosition(rift.Comp2), worldToView);
        var radius = rift.Comp1.Radius * MinimapScale;

        handle.DrawCircle(centre, radius, HyperspaceRiftSystem.RadarColor.WithAlpha(0.08f));
        handle.DrawCircle(centre, radius, HyperspaceRiftSystem.RadarColor, filled: false);

        var text = Loc.GetString("shuttle-console-hyperspace-rift");
        var dimensions = handle.GetDimensions(Font, text, 1f);
        var labelPos = Vector2.Clamp(centre - dimensions / 2f, Vector2.Zero, PixelSize - dimensions);
        handle.DrawString(Font, labelPos, text, HyperspaceRiftSystem.RadarColor);
    }
}
