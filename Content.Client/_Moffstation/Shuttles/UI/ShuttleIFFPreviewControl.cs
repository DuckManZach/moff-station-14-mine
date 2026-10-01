using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared.Shuttles.Systems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Map.Components;

namespace Content.Client._Moffstation.Shuttles.UI;

/// <summary>
/// Draws a single grid fitted to the control, oriented with its console.
/// </summary>
public sealed partial class ShuttleIFFPreviewControl : BaseShuttleControl
{
    private const float FitFraction = 0.9f;

    private readonly SharedShuttleSystem _shuttles;
    private readonly SharedTransformSystem _transform;

    private EntityUid? _shuttle;
    private EntityUid? _console;

    public ShuttleIFFPreviewControl()
    {
        SetSize = new Vector2(232, 120);
        MouseFilter = MouseFilterMode.Ignore;
        _shuttles = EntManager.System<SharedShuttleSystem>();
        _transform = EntManager.System<SharedTransformSystem>();
    }

    public void SetShuttle(EntityUid? shuttle)
    {
        _shuttle = shuttle;
    }

    public void SetConsole(EntityUid? console)
    {
        _console = console;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!EntManager.TryGetComponent(_shuttle, out MapGridComponent? grid))
            return;

        // Console-up, like the nav radar, so the preview holds still while the shuttle turns.
        var consoleRotation = _console != null && EntManager.EntityExists(_console)
            ? _transform.GetWorldRotation(_console.Value) - _transform.GetWorldRotation(_shuttle.Value)
            : Angle.Zero;
        var rotation = Matrix3Helpers.CreateTransform(Vector2.Zero, -consoleRotation);
        var bounds = rotation.TransformBox(grid.LocalAABB);

        if (bounds.Width <= 0f || bounds.Height <= 0f)
            return;

        var scale = MathF.Min(PixelWidth / bounds.Width, PixelHeight / bounds.Height) * FitFraction;
        var gridToView = rotation
                         * Matrix3x2.CreateTranslation(-bounds.Center)
                         * Matrix3x2.CreateScale(scale, -scale)
                         * Matrix3x2.CreateTranslation(PixelSize / 2f);

        DrawGrid(handle, gridToView, (_shuttle.Value, grid), _shuttles.GetIFFColor(_shuttle.Value, self: true), alpha: 0.1f);
    }
}
