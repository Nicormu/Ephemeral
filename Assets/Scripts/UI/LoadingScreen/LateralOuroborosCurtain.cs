using System.Collections;
using UnityEngine;

/// <summary>
/// Lateral curtain transition: two RectTransforms — Head (left) and Tail (right) — slide via
/// anchoredPosition between an explicit OPEN (off-screen) position and an explicit CLOSED
/// (meeting at center) position. The two halves are just butted together at the seam when
/// closed — no masking/reveal involved.
///
/// Used two ways, per-scene, since Canvas Manager is NOT persistent across scene loads:
///   - EXIT scene (the one being left): panels authored at their OPEN position. SceneLoader
///     calls Close() before activating the new scene. Never opens itself — the object is
///     destroyed with the old scene right after closing.
///   - ENTRY scene (the one being loaded into): panels authored at their CLOSED position,
///     matching what's already on screen from the exit scene's close, so the cut is invisible.
///     Set _openOnStart to have it open itself automatically after _openStartDelay.
/// </summary>
public class LateralOuroborosCurtain : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("Left panel — ouroboros HEAD.")]
    [SerializeField] private RectTransform _headPanel;

    [Tooltip("Right panel — ouroboros TAIL.")]
    [SerializeField] private RectTransform _tailPanel;

    [Header("Open (off-screen) Positions")]
    [SerializeField] private Vector2 _headOpenPosition;
    [SerializeField] private Vector2 _tailOpenPosition;

    [Header("Closed (resting, meeting at center) Positions")]
    [SerializeField] private Vector2 _headClosedPosition;
    [SerializeField] private Vector2 _tailClosedPosition;

    [Header("Timing")]
    [SerializeField] private float _closeDuration = 0.6f;
    [SerializeField] private float _openDuration = 0.6f;

    [Header("Entry Scene Behavior")]
    [Tooltip("If true, this curtain plays its own Open() automatically on Start — use this on the scene being LOADED INTO, where panels are authored already at their Closed position. Leave off on the scene being left, where SceneLoader drives Close() manually.")]
    [SerializeField] private bool _openOnStart = false;

    [Tooltip("Brief pause before the automatic opening starts, so the new scene has a moment to settle before the curtain begins sliding away. Only used if Open On Start is enabled.")]
    [SerializeField] private float _openStartDelay = 0.15f;

    /// <summary>True once both panels have reached their closed (meeting) position.</summary>
    public bool IsClosed { get; private set; }

    private Coroutine _routine;

    private void Start()
    {
        if (_openOnStart)
            StartCoroutine(DelayedOpen());
    }

    private IEnumerator DelayedOpen()
    {
        if (_openStartDelay > 0f)
            yield return new WaitForSecondsRealtime(_openStartDelay);

        Open();
    }

    /// <summary>Slides both panels inward to meet at the center. onComplete fires once fully closed.</summary>
    public void Close(System.Action onComplete = null)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(toClosed: true, _closeDuration,
            () => { IsClosed = true; onComplete?.Invoke(); }));
    }

    /// <summary>Slides both panels back out to their off-screen open position. onComplete fires once fully open.</summary>
    public void Open(System.Action onComplete = null)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(toClosed: false, _openDuration,
            () => { IsClosed = false; onComplete?.Invoke(); }));
    }

    private IEnumerator Animate(bool toClosed, float duration, System.Action onComplete)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        Vector2 headFrom = _headPanel != null ? _headPanel.anchoredPosition : Vector2.zero;
        Vector2 tailFrom = _tailPanel != null ? _tailPanel.anchoredPosition : Vector2.zero;

        Vector2 headTo = toClosed ? _headClosedPosition : _headOpenPosition;
        Vector2 tailTo = toClosed ? _tailClosedPosition : _tailOpenPosition;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled — same reasoning as ExitMenu's eyelids
            float t = Mathf.Clamp01(elapsed / duration);

            float eased = toClosed ? AnimationEasings.EaseInQuad(t) : AnimationEasings.EaseOutQuad(t);

            if (_headPanel != null) _headPanel.anchoredPosition = Vector2.Lerp(headFrom, headTo, eased);
            if (_tailPanel != null) _tailPanel.anchoredPosition = Vector2.Lerp(tailFrom, tailTo, eased);

            yield return null;
        }

        if (_headPanel != null) _headPanel.anchoredPosition = headTo;
        if (_tailPanel != null) _tailPanel.anchoredPosition = tailTo;

        _routine = null;
        onComplete?.Invoke();
    }
}