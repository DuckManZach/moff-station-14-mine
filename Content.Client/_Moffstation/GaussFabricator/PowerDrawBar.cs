using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Moffstation.GaussFabricator;

/// <summary>
/// Horizontal bar showing received power (bright) and configured-but-unreceived power (dim),
/// with tick marks at regular watt intervals — modelled after BeakerBarChart.
/// </summary>
public sealed class PowerDrawBar : Control
{
    // Watts between each small notch.
    public float NotchInterval = 10_000f;
    // Every N small notches becomes a medium notch.
    public int MediumNotchDivisor = 5;

    public float SmallNotchHeight  = 0.15f;
    public float MediumNotchHeight = 0.35f;

    // If the pixel distance between small notches falls below this, scale up by ScaleMultiplier.
    public int MinSmallNotchScreenDistance = 3;
    public int ScaleMultiplier = 5;

    public Color ReceivedColor   = Color.FromHex("#4499ff");
    public Color GapColor        = Color.FromHex("#1a4a8a");
    public Color BackgroundColor = new(0.1f, 0.1f, 0.1f);
    public Color NotchColor      = new(1f, 1f, 1f, 0.3f);

    private float _maxRate;
    private float _targetReceived;
    private float _targetConfigured;
    private float _displayedReceived;
    private float _displayedConfigured;

    public void SetValues(float maxRate, float received, float configured)
    {
        _maxRate          = maxRate;
        _targetReceived   = Math.Clamp(received,   0f, maxRate);
        _targetConfigured = Math.Clamp(configured, 0f, maxRate);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        const float tweenInverseHalfLife = 8f;
        var factor = MathHelper.Clamp01(tweenInverseHalfLife * args.DeltaSeconds);

        _displayedReceived   = MathHelper.Lerp(_displayedReceived,   _targetReceived,   factor);
        _displayedConfigured = MathHelper.Lerp(_displayedConfigured, _targetConfigured, factor);

        if (MathF.Abs(_displayedReceived   - _targetReceived)   < 0.5f)
            _displayedReceived   = _targetReceived;
        if (MathF.Abs(_displayedConfigured - _targetConfigured) < 0.5f)
            _displayedConfigured = _targetConfigured;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, BackgroundColor);

        if (_maxRate <= 0f)
            return;

        var receivedX = _displayedReceived   / _maxRate * PixelWidth;
        var targetX   = _displayedConfigured / _maxRate * PixelWidth;

        // Configured-but-unreceived gap (dim blue)
        if (targetX > receivedX)
            handle.DrawRect(new UIBox2(receivedX, 0, targetX, PixelHeight), GapColor);

        // Actually received power (bright blue)
        if (receivedX > 0f)
            handle.DrawRect(new UIBox2(0, 0, receivedX, PixelHeight), ReceivedColor);

        // Tick marks — scale up the interval if notches would be too crowded
        var notchInterval   = NotchInterval;
        var notchPixelWidth = notchInterval / _maxRate * PixelWidth;

        while (notchPixelWidth < MinSmallNotchScreenDistance && notchInterval < _maxRate)
        {
            notchInterval   *= ScaleMultiplier;
            notchPixelWidth *= ScaleMultiplier;
        }

        for (var w = notchInterval; w <= _maxRate; w += notchInterval)
        {
            var x            = w / _maxRate * PixelWidth;
            var index        = (int) MathF.Round(w / NotchInterval);
            var isMedium     = index % MediumNotchDivisor == 0;
            var notchHeight  = (isMedium ? MediumNotchHeight : SmallNotchHeight) * PixelHeight;

            handle.DrawLine(new Vector2(x, PixelHeight), new Vector2(x, PixelHeight - notchHeight), NotchColor);
        }
    }
}
