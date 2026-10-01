using System.Numerics;
using Content.Shared._Moffstation.Shuttles.Components;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleNavControl
{
    private const float ThrusterScale = 0.4f;
    private const float PlumeLength = 1.8f;
    private const float PlumeWidth = 0.3f;

    private static readonly Color ThrusterIdleColor = Color.Orange.WithAlpha(0.3f);
    private static readonly Color ThrusterFiringColor = Color.FromHex("#ffb347");

    private readonly Vector2[] _thrusterQuad = new Vector2[4];
    private readonly Vector2[] _thrusterPlume = new Vector2[3];

    public bool ShowThrusters { get; set; } = true;

    /// <summary>
    /// Draws a grid's powered thrusters, with a plume out of the nozzle of any that are firing.
    /// </summary>
    private void DrawThrusters(DrawingHandleScreen handle, EntityUid gridUid, Matrix3x2 gridToView)
    {
        if (!ShowThrusters || !EntManager.TryGetComponent(gridUid, out ShuttleThrusterRadarComponent? radar))
            return;

        var viewBounds = new Box2(-PlumeLength * MinimapScale, -PlumeLength * MinimapScale,
            PixelWidth + PlumeLength * MinimapScale, PixelHeight + PlumeLength * MinimapScale);

        var quad = _thrusterQuad;
        var plume = _thrusterPlume;

        foreach (var thruster in radar.Thrusters)
        {
            if (!viewBounds.Contains(Vector2.Transform(thruster.LocalPosition, gridToView)))
                continue;

            quad[0] = Vector2.Transform(thruster.LocalPosition + thruster.LocalRotation.RotateVec(new Vector2(-ThrusterScale, -ThrusterScale)), gridToView);
            quad[1] = Vector2.Transform(thruster.LocalPosition + thruster.LocalRotation.RotateVec(new Vector2(ThrusterScale, -ThrusterScale)), gridToView);
            quad[2] = Vector2.Transform(thruster.LocalPosition + thruster.LocalRotation.RotateVec(new Vector2(ThrusterScale, ThrusterScale)), gridToView);
            quad[3] = Vector2.Transform(thruster.LocalPosition + thruster.LocalRotation.RotateVec(new Vector2(-ThrusterScale, ThrusterScale)), gridToView);

            if (!thruster.Firing)
            {
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, quad, ThrusterIdleColor);
                continue;
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, quad, ThrusterFiringColor);

            var nozzle = thruster.LocalRotation.Opposite().ToWorldVec();
            var side = new Vector2(-nozzle.Y, nozzle.X) * PlumeWidth;
            var nozzleCentre = thruster.LocalPosition + nozzle * ThrusterScale;

            plume[0] = Vector2.Transform(nozzleCentre + side, gridToView);
            plume[1] = Vector2.Transform(nozzleCentre - side, gridToView);
            plume[2] = Vector2.Transform(thruster.LocalPosition + nozzle * PlumeLength, gridToView);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, plume, ThrusterFiringColor.WithAlpha(0.6f));
        }
    }
}
