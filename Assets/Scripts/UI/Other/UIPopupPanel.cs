using System.Collections;
using UnityEngine;

/// <summary>
/// Generic reusable popup panel: fades a CanvasGroup in/out on Show()/Hide(). Pair with a child
/// UIPopupBackdrop (a full-screen Image behind the actual panel content) to also get
/// click-outside-to-close for free — see UIPopupBackdrop's own doc for the required hierarchy.
///
/// Used today by ExitMenu's confirmation panel; intended to be reused as-is for future popups
/// (Credits, Settings, etc.) that want the same fade + click-outside-to-close behavior — just
/// add a CanvasGroup + this component to the panel root.
///
/// Stays fully ACTIVE (GameObject.SetActive is never touched) at all times — "hidden" is purely
/// alpha 0 + CanvasGroup blocking input. This is what lets Show()/Hide() safely interrupt a
/// fade already in progress (e.g. rapid clicking) instead of a coroutine getting killed out from
/// under it by the GameObject deactivating mid-fade.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPopupPanel : MonoBehaviour
{
    [Tooltip("Seconds for the fade in/out.")]
    [SerializeField] private float _fadeDuration = 0.2f;

    [Tooltip("Start already faded in when the scene loads. Leave off for popups that should start hidden — the normal case (e.g. a confirmation dialog).")]
    [SerializeField] private bool _startVisible = false;

    private CanvasGroup _canvasGroup;
    private Coroutine _fadeRoutine;

    /// <summary>True once a fade-in has fully completed. False while hidden OR mid-fade.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>Fired once a Show() fade-in finishes.</summary>
    public event System.Action OnShown;

    /// <summary>Fired once a Hide() fade-out finishes.</summary>
    public event System.Action OnHidden;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        SetVisibleInstant(_startVisible);
    }

    /// <summary>Fades this panel in and makes it interactable once fully visible. Safe to call
    /// while already fading — restarts the fade from wherever it currently is.</summary>
    public void Show()
    {
        if (IsVisible && _fadeRoutine == null) return; // already fully shown, nothing to do

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Fade(1f, () => OnShown?.Invoke()));
    }

    /// <summary>Fades this panel out and blocks interaction immediately (not just once the fade
    /// finishes — a half-faded panel should never still be clickable). Safe to call while
    /// already fading, or while already hidden (no-ops).</summary>
    public void Hide()
    {
        if (!IsVisible && _fadeRoutine == null) return; // already fully hidden, nothing to do

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Fade(0f, () => OnHidden?.Invoke()));
    }

    private IEnumerator Fade(float targetAlpha, System.Action onComplete)
    {
        // Block input for the ENTIRE fade, both directions — a panel that's only 40% faded in
        // shouldn't be clickable yet, and a panel that's fading out shouldn't remain clickable
        // either. Re-evaluated to the correct final state once the fade completes below.
        _canvasGroup.interactable = false;

        float startAlpha = _canvasGroup.alpha;
        float duration = Mathf.Max(0.01f, _fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled so menus still animate if gameplay is paused
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        IsVisible = targetAlpha >= 1f;

        _canvasGroup.interactable = IsVisible;
        _canvasGroup.blocksRaycasts = IsVisible; // hidden panel must stop blocking clicks to whatever's behind it

        _fadeRoutine = null;
        onComplete?.Invoke();
    }

    private void SetVisibleInstant(bool visible)
    {
        IsVisible = visible;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }
}