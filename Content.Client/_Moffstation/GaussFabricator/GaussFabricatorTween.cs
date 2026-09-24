using Robust.Shared.Maths;

namespace Content.Client._Moffstation.GaussFabricator;

/// <summary>
/// Shared easing for the fabricator's readouts, so every bar in the window moves at the same rate.
/// </summary>
public static class GaussFabricatorTween
{
    /// <summary>
    /// How quickly a displayed value closes the gap to its target. Higher is snappier.
    /// </summary>
    private const float InverseHalfLife = 8f;

    /// <summary>
    /// Eases the displayed value toward the target, snapping onto it once the gap is under epsilon.
    /// </summary>
    public static float Approach(float displayed, float target, float deltaSeconds, float epsilon)
    {
        var factor = MathHelper.Clamp01(InverseHalfLife * deltaSeconds);
        var next = MathHelper.Lerp(displayed, target, factor);

        return MathF.Abs(next - target) < epsilon ? target : next;
    }
}