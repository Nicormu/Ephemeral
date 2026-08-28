using UnityEngine;

/// <summary>
/// Shared animation easing curves used across UI components. Pure static utilities — no
/// runtime dependencies on Unity objects, so they can be called from Update(), Coroutines,
/// or any context that needs smooth interpolation.
/// </summary>
public static class AnimationEasings
{
    // ── Constants ───────────────────────────────────────────────

    /// <summary>Standard "ease out back" overshoot constant. Matches Unity's built-in value.</summary>
    public const float EaseOutBackC1 = 1.70158f;

    // ── Common curves ───────────────────────────────────────────

    /// <summary>No easing — linear interpolation (identity curve).</summary>
    public static float Linear(float t) => t;

    /// <summary>Gently accelerates from 0 to 1.</summary>
    public static float EaseInQuad(float t) => t * t;

    /// <summary>Starts fast, then decelerates to a smooth stop.</summary>
    public static float EaseOutQuad(float t) => t * (2f - t);

    /// <summary>Smooth S-curve: eases in and out symmetrically.</summary>
    public static float EaseInOutQuad(float t)
    {
        return t < 0.5f
            ? 2f * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
    }

    /// <summary>Starts fast, decelerates smoothly — the go-to for most UI slides.</summary>
    public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3);

    /// <summary>Fast start, very soft landing — good for quick pop-ins.</summary>
    public static float EaseOutQuart(float t) => 1f - Mathf.Pow(1f - t, 4);

    /// <summary>Overshoots past the target then settles back — springy feel.</summary>
    public static float EaseOutBack(float t)
    {
        var c3 = EaseOutBackC1 + 1f;
        var x = t - 1f;
        return 1f + c3 * (x * x * x) + EaseOutBackC1 * (x * x);
    }

    // ── Unity built-in equivalents ──────────────────────────────

    /// <summary>Convenience wrapper around Unity's built-in curves.</summary>
    public static float SmoothStep(float t) => Mathf.SmoothStep(0f, 1f, t);

    /// <summary>Convenience wrapper around Unity's built-in curve.</summary>
    public static float SinusoidalEaseOut(float t) => Mathf.Sin(t * Mathf.PI * 0.5f);
}
