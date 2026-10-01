using System.Numerics;
using Content.Client._Moffstation.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleMapControl
{
    // Below this on-screen size a grid's hull is unreadable, so it gets a marker instead.
    private const float MinGridShapePixels = 8f;

    private HyperspaceRiftSystem? _rifts;

    /// <summary>
    /// Draws the grid's hull, or returns false if it is too small on screen to be worth drawing.
    /// </summary>
    private bool TryDrawGridShape(DrawingHandleScreen handle, Entity<MapGridComponent> grid, Matrix3x2 mapToOffset, Color color)
    {
        if (grid.Comp.LocalAABB.MaxDimension * MinimapScale < MinGridShapePixels * UIScale)
            return false;

        var gridToView = _xformSystem.GetWorldMatrix(grid)
                         * mapToOffset
                         * Matrix3x2.CreateScale(MinimapScale, -MinimapScale)
                         * Matrix3x2.CreateTranslation(MidPointVector);

        DrawGrid(handle, gridToView, grid, color, fillColor: GetGridFill(color));
        return true;
    }

    private void DrawRift(DrawingHandleScreen handle, EntityUid mapUid, Matrix3x2 mapToOffset)
    {
        _rifts ??= EntManager.System<HyperspaceRiftSystem>();

        if (!_rifts.TryGetRift(mapUid, out var rift))
            return;

        var relativePos = Vector2.Transform(_xformSystem.GetWorldPosition(rift.Comp2), mapToOffset);
        var uiPos = ScalePosition(relativePos with { Y = -relativePos.Y });
        var radius = rift.Comp1.Radius * MinimapScale;

        handle.DrawCircle(uiPos, radius, HyperspaceRiftSystem.RadarColor.WithAlpha(0.08f));
        handle.DrawCircle(uiPos, radius, HyperspaceRiftSystem.RadarColor, filled: false);

        var text = Loc.GetString("shuttle-console-hyperspace-rift");
        var dimensions = handle.GetDimensions(_font, text, 1f);
        handle.DrawString(_font, GetEdgeLabelPosition(uiPos, dimensions), text, HyperspaceRiftSystem.RadarColor);
    }

    /// <summary>
    /// Labels grids and beacons that are off screen at the edge of the map, in their direction.
    /// </summary>
    private void DrawOffscreenLabels(DrawingHandleScreen handle, List<IMapObject> mapObjects, List<IMapObject> viewportObjects, Matrix3x2 mapToOffset)
    {
        foreach (var mapObj in mapObjects)
        {
            if (viewportObjects.Contains(mapObj))
                continue;

            string? text;
            Color color;
            Vector2 worldPos;

            switch (mapObj)
            {
                case GridMapObject gridObj when EntManager.HasComponent<MapGridComponent>(gridObj.Entity) &&
                                                !EntManager.HasComponent<MapComponent>(gridObj.Entity):
                    EntManager.TryGetComponent(gridObj.Entity, out IFFComponent? iffComp);

                    if (gridObj.Entity != _shuttleEntity && iffComp != null &&
                        (iffComp.Flags & (IFFFlags.Hide | IFFFlags.HideLabel)) != 0x0)
                    {
                        continue;
                    }

                    text = _shuttles.GetIFFLabel(gridObj.Entity, self: true, component: iffComp);
                    color = Color.FromSrgb(_shuttles.GetIFFColor(gridObj.Entity, self: _shuttleEntity == gridObj.Entity, component: iffComp));
                    worldPos = Maps.GetGridPosition(gridObj.Entity);
                    break;
                case ShuttleBeaconObject beacon when ShowBeacons:
                    text = beacon.Name;
                    color = Color.AliceBlue;
                    worldPos = _xformSystem.ToMapCoordinates(EntManager.GetCoordinates(beacon.Coordinates)).Position;
                    break;
                default:
                    continue;
            }

            if (string.IsNullOrEmpty(text))
                continue;

            var relativePos = Vector2.Transform(worldPos, mapToOffset);
            var uiPos = ScalePosition(relativePos with { Y = -relativePos.Y });
            var dimensions = handle.GetDimensions(_font, text, 1f);
            handle.DrawString(_font, GetEdgeLabelPosition(uiPos, dimensions), text, color);
        }
    }

    /// <summary>
    /// Centres a label on a point, or pushes it to the edge of the control towards the point if it is off screen.
    /// </summary>
    private Vector2 GetEdgeLabelPosition(Vector2 uiPos, Vector2 dimensions)
    {
        // Scaling the offset from the centre keeps the label in the point's direction instead of hugging a corner.
        var offset = uiPos / PixelSize - new Vector2(0.5f, 0.5f);
        var offsetMax = Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) * 2f;

        if (offsetMax > 1f)
            uiPos = (offset / offsetMax + new Vector2(0.5f, 0.5f)) * PixelSize;

        return Vector2.Clamp(uiPos - dimensions / 2f, Vector2.Zero, PixelSize - dimensions);
    }
}
