using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared full-screen fade-to-black utility. Owns a single full-screen Image and exposes simple
/// FadeToBlack/FadeFromBlack coroutines so multiple systems (PlayerDeathScreen,
/// DungeonResetController) can reuse the same panel/logic instead of each maintaining its own.
/// SetAlpha() additionally lets a caller drive the fade frame-by-frame itself (e.g.
/// DungeonResetController's hold-to-restart, which ties fade progress directly to how long a key
/// has been held) instead of only doing fixed-duration fades.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Tooltip("Full-screen black Image. Anchors stretched to fill the screen. This script drives its alpha directly.")]
    [SerializeField] private Image _fadePanel;

    /// <summary>
    /// True once the panel's alpha has reached fully opaque (via FadeToBlack completing, or
    /// SetAlpha(1)) and hasn't been brought back down since. Lets a caller (DungeonResetController)
    /// check "is the screen already black?" before deciding whether it needs to fade in itself,
    /// e.g. when triggered right after PlayerDeathScreen's own fade-in.
    /// </summary>
    public bool IsBlack { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_fadePanel != null)
        {
            Color c = _fadePanel.color;
            c.a = 0f;
            _fadePanel.color = c;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Fades the panel from its current alpha to fully opaque (black).</summary>
    public IEnumerator FadeToBlack(float duration)
    {
        yield return FadeTo(1f, duration);
        IsBlack = true;
    }

    /// <summary>Fades the panel from its current alpha to fully transparent.</summary>
    public IEnumerator FadeFromBlack(float duration)
    {
        yield return FadeTo(0f, duration);
        IsBlack = false;
    }

    /// <summary>Directly sets the panel's alpha (0 = clear, 1 = fully black) with no coroutine/
    /// duration — for callers that want to drive the fade themselves frame-by-frame (e.g. tying
    /// it to how long a key has been held). Updates IsBlack to match.</summary>
    public void SetAlpha(float alpha)
    {
        if (_fadePanel == null) return;

        alpha = Mathf.Clamp01(alpha);
        Color c = _fadePanel.color;
        c.a = alpha;
        _fadePanel.color = c;

        IsBlack = alpha >= 1f;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (_fadePanel == null) yield break;

        float startAlpha = _fadePanel.color.a;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Color c = _fadePanel.color;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            _fadePanel.color = c;

            yield return null;
        }

        Color final = _fadePanel.color;
        final.a = targetAlpha;
        _fadePanel.color = final;
    }
}