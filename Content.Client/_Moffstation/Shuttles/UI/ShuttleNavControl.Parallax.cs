using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleNavControl
{
    public bool ShowParallax { get; set; }

    /// <summary>
    /// Draws the radar's map parallax, rotated with the radar.
    /// </summary>
    private void DrawParallax(DrawingHandleScreen handle, EntityCoordinates coordinates, Angle rotation)
    {
        if (!EntManager.TryGetComponent(coordinates.EntityId, out TransformComponent? xform))
            return;

        var mapPos = _transform.ToMapCoordinates(coordinates);
        var viewRotation = (RotateWithEntity ? _transform.GetWorldRotation(xform) : rotation) + rotation;

        DrawMapParallax(handle, mapPos.MapId, mapPos.Position, viewRotation);
    }
}
