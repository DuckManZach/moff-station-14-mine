using System.Numerics;
using Content.Shared._Moffstation.GaussFabricator;
using Content.Shared.Destructible.Thresholds;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Moffstation.GaussFabricator;

/// <summary>
/// Vertical gauge plotting a live atmospheric reading against an acceptable band and a nested optimal band.
/// </summary>
public sealed partial class AtmosRangeBar : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    // Fraction of the acceptable span shown beyond each end, so the bad thresholds sit inside the gauge.
    private const float DisplayPadding = 0.2f;

    private const float LabelMargin = 2f;

    private const float ReadingSnapEpsilon = 0.01f;

    private readonly Color _backgroundColor = new(0.1f, 0.1f, 0.1f);
    private readonly Color _acceptableColor = Color.FromHex("#20304a");
    private readonly Color _optimalFillColor = Color.FromHex("#2fbf4c").WithAlpha(0.3f);
    private readonly Color _optimalColor = Color.FromHex("#3ee85f");
    private readonly Color _badColor = Color.FromHex("#e03050");
    private readonly Color _currentColor = Color.FromHex("#4499ff");

    private readonly Font _font;

    private MinMax _acceptable;
    private MinMax _optimal;
    private float? _targetCurrent;
    private float? _displayedCurrent;

    /// <summary>
    /// Numeric format for the values drawn inside the gauge.
    /// </summary>
    public string ValueFormat { get; set; } = "F0";

    public string NoDataText { get; set; } = "N/A";

    public AtmosRangeBar()
    {
        IoCManager.InjectDependencies(this);

        var fontResource = _resourceCache.GetResource<FontResource>("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf");
        _font = new VectorFont(fontResource, 10);
    }

    public void SetValue(GaussFabricatorGauge gauge)
    {
        _acceptable = gauge.Acceptable;
        _optimal = gauge.Optimal;
        _targetCurrent = gauge.Current;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (_targetCurrent is not { } target)
        {
            _displayedCurrent = null;
            return;
        }

        // Snap rather than tween up from zero the first time we get a reading.
        if (_displayedCurrent is not { } displayed)
        {
            _displayedCurrent = target;
            return;
        }

        _displayedCurrent = GaussFabricatorTween.Approach(displayed, target, args.DeltaSeconds, ReadingSnapEpsilon);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, _backgroundColor);

        float span = _acceptable.Max - _acceptable.Min;
        if (span <= 0f)
            return;

        var displayMin = _acceptable.Min - span * DisplayPadding;
        var displayMax = _acceptable.Max + span * DisplayPadding;

        var minAcceptableY = ValueToY(_acceptable.Min, displayMin, displayMax);
        var maxAcceptableY = ValueToY(_acceptable.Max, displayMin, displayMax);
        var minOptimalY = ValueToY(_optimal.Min, displayMin, displayMax);
        var maxOptimalY = ValueToY(_optimal.Max, displayMin, displayMax);

        handle.DrawRect(new UIBox2(0, maxAcceptableY, PixelWidth, minAcceptableY), _acceptableColor);
        handle.DrawRect(new UIBox2(0, maxOptimalY, PixelWidth, minOptimalY), _optimalFillColor);

        DrawThreshold(handle, maxAcceptableY, _badColor);
        DrawThreshold(handle, minAcceptableY, _badColor);
        DrawThreshold(handle, maxOptimalY, _optimalColor);
        DrawThreshold(handle, minOptimalY, _optimalColor);

        DrawLabel(handle, _optimal.Max.ToString(ValueFormat), maxOptimalY, _optimalColor, above: true, right: false);
        DrawLabel(handle, _optimal.Min.ToString(ValueFormat), minOptimalY, _optimalColor, above: false, right: false);

        if (_displayedCurrent is not { } current)
        {
            DrawLabel(handle, NoDataText, PixelHeight / 2f, _currentColor, above: false, right: true);
            return;
        }

        var currentY = ValueToY(current, displayMin, displayMax);
        DrawThreshold(handle, currentY, _currentColor);

        // Right-aligned so it can never collide with the left-aligned optimal bounds.
        DrawLabel(handle, current.ToString(ValueFormat), currentY, _currentColor, above: currentY > PixelHeight / 2f, right: true);
    }

    private void DrawThreshold(DrawingHandleScreen handle, float y, Color color)
    {
        var thickness = MathF.Max(2f, MathF.Round(2f * UIScale));
        var top = MathF.Round(y - thickness / 2f);
        handle.DrawRect(new UIBox2(0, top, PixelWidth, top + thickness), color);
    }

    private void DrawLabel(DrawingHandleScreen handle, string text, float y, Color color, bool above, bool right)
    {
        var dimensions = handle.GetDimensions(_font, text, UIScale);
        var inset = LabelMargin * UIScale;
        var x = right ? PixelWidth - dimensions.X - inset : inset;
        var top = above ? y - dimensions.Y - LabelMargin * UIScale : y + LabelMargin * UIScale;

        handle.DrawString(
            _font,
            new Vector2(x, Math.Clamp(top, 0f, MathF.Max(0f, PixelHeight - dimensions.Y))),
            text,
            UIScale,
            color);
    }

    private float ValueToY(float value, float displayMin, float displayMax)
    {
        var fraction = MathHelper.Clamp01((value - displayMin) / (displayMax - displayMin));
        return PixelHeight * (1f - fraction);
    }
}
