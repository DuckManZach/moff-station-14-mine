using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Moffstation.GaussFabricator;

public sealed class PowerDrawBar : Control
{
    // watt interval between small notches (minecraft)
    private const float NotchInterval = 10_000f;

    // interval of big notches measured in small notches
    private const int BigNotchDivisor = 5;

    private const float SmallNotchHeight = 0.15f;
    private const float MediumNotchHeight = 0.35f;

    // Adjusting in case the notches are too crowded, probably not needed but may be useful if we change shit in the future
    private const int MinSmallNotchScreenDistance = 3;
    private const int ScaleMultiplier = 5;

    private readonly Color _receivedColor = Color.FromHex("#4499ff");
    private readonly Color _gapColor = Color.FromHex("#1a4a8a");
    private readonly Color _backgroundColor = new(0.1f, 0.1f, 0.1f);
    private readonly Color _notchColor = new(1f, 1f, 1f, 0.3f);

    private float _maxRate;
    private float _targetReceived;
    private float _targetConfigured;
    private float _displayedReceived;
    private float _displayedConfigured;

    public void SetValues(float maxRate, float received, float configured)
    {
        if (maxRate < 0f || received < 0f || configured < 0f)
            return;

        _maxRate = maxRate;
        _targetReceived = Math.Clamp(received, 0f, maxRate);
        _targetConfigured = Math.Clamp(configured, 0f, maxRate);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        const float tweenInverseHalfLife = 8f;
        var factor = MathHelper.Clamp01(tweenInverseHalfLife * args.DeltaSeconds);

        _displayedReceived = MathHelper.Lerp(_displayedReceived, _targetReceived, factor);
        _displayedConfigured = MathHelper.Lerp(_displayedConfigured, _targetConfigured, factor);

        if (MathF.Abs(_displayedReceived - _targetReceived) < 0.5f)
            _displayedReceived = _targetReceived;
        if (MathF.Abs(_displayedConfigured - _targetConfigured) < 0.5f)
            _displayedConfigured = _targetConfigured;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, _backgroundColor);

        if (_maxRate <= 0f)
            return;

        var receivedX = _displayedReceived / _maxRate * PixelWidth;
        var targetX = _displayedConfigured / _maxRate * PixelWidth;

        // configured draw rate
        if (targetX > receivedX)
            handle.DrawRect(new UIBox2(receivedX, 0, targetX, PixelHeight), _gapColor);

        // actually received draw rate
        if (receivedX > 0f)
            handle.DrawRect(new UIBox2(0, 0, receivedX, PixelHeight), _receivedColor);

        var notchInterval = NotchInterval;
        var notchPixelWidth = notchInterval / _maxRate * PixelWidth;

        while (notchPixelWidth < MinSmallNotchScreenDistance && notchInterval < _maxRate)
        {
            notchInterval *= ScaleMultiplier;
            notchPixelWidth *= ScaleMultiplier;
        }

        for (var w = notchInterval; w <= _maxRate; w += notchInterval)
        {
            var x = w / _maxRate * PixelWidth;
            var index = (int) MathF.Round(w / notchInterval);
            var isMedium = index % BigNotchDivisor == 0;
            var notchHeight = (isMedium ? MediumNotchHeight : SmallNotchHeight) * PixelHeight;

            handle.DrawLine(new Vector2(x, PixelHeight), new Vector2(x, PixelHeight - notchHeight), _notchColor);
        }
    }
}
