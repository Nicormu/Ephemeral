using System.Collections;
using UnityEngine;

/// <summary>
/// Flow:
///   1. Exit button calls ShowConfirmation() — shows the "Are you sure?" panel.
///   2. Yes button calls ConfirmExit() — hides the panel, plays the eyelid-close animation,
///      then quits.
///   3. No button calls CancelExit() — just hides the panel, nothing else happens.
///
/// The eyelid animation is driven entirely in code: two black UI panels (top/bottom) grow from
/// height 0 to fully covering the screen, meeting in the middle like closing eyelids.
/// </summary>
public class ExitMenu : MonoBehaviour
{
    [Header("Confirmation UI")]
    [Tooltip("The 'Are you sure you want to exit?' panel. Should start inactive in the scene.")]
    [SerializeField] private GameObject _confirmationPanel;

    [Header("Eyelid Animation")]
    [Tooltip("Parent GameObject holding both eyelid Images. Should start inactive in the scene.")]
    [SerializeField] private GameObject _eyelidOverlay;

    [Tooltip("RectTransform anchored to the TOP edge.")]
    [SerializeField] private RectTransform _topEyelid;

    [Tooltip("RectTransform anchored to the BOTTOM edge.")]
    [SerializeField] private RectTransform _bottomEyelid;

    [Tooltip("How tall each eyelid grows, in pixels (UI space).")]
    [SerializeField] private float _eyelidTargetHeight = 650f;

    [Tooltip("Seconds for the eyelids to close completely.")]
    [SerializeField] private float _closeDuration = 0.6f;

    [Tooltip("Extra pause after the eyelids are fully closed, before actually quitting — avoids an abrupt cut right as the screen goes black.")]
    [SerializeField] private float _holdBeforeQuit = 0.3f;

    private bool _isClosing;

    private void Awake()
    {
        if (_confirmationPanel != null) _confirmationPanel.SetActive(false);
        if (_eyelidOverlay != null) _eyelidOverlay.SetActive(false);

        if (_topEyelid != null) SetEyelidHeight(_topEyelid, 0f);
        if (_bottomEyelid != null) SetEyelidHeight(_bottomEyelid, 0f);
    }

    /// <summary>
    /// Call from the main Exit button's OnClick.
    /// </summary>
    public void ShowConfirmation()
    {
        if (_isClosing) return; // already quitting — ignore stray clicks

        if (_confirmationPanel == null)
        {
            Debug.LogWarning("[Exit] No confirmation panel assigned — quitting immediately instead.");
            ConfirmExit();
            return;
        }

        _confirmationPanel.SetActive(true);
    }

    /// <summary>
    /// Call from the confirmation panel's "No" / cancel button.
    /// </summary>
    public void CancelExit()
    {
        if (_confirmationPanel != null) _confirmationPanel.SetActive(false);
    }

    /// <summary>
    /// Call from the confirmation panel's "Yes" button.
    /// </summary>
    public void ConfirmExit()
    {
        if (_isClosing) return;

        if (_confirmationPanel != null) _confirmationPanel.SetActive(false);

        StartCoroutine(PlayEyelidCloseThenQuit());
    }

    private IEnumerator PlayEyelidCloseThenQuit()
    {
        _isClosing = true;

        if (_topEyelid == null || _bottomEyelid == null || _eyelidOverlay == null)
        {
            Debug.LogWarning("[Exit] Eyelid references not fully assigned — skipping animation and quitting directly.");
            QuitNow();
            yield break;
        }

        _eyelidOverlay.SetActive(true);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _closeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled so this still plays if something paused Time.timeScale
            float t = Mathf.Clamp01(elapsed / duration);

            // Ease-in (t*t): starts slow, accelerates toward closure — reads like a blink
            // snapping shut rather than a mechanical linear slide.
            float eased = t * t;
            float height = Mathf.Lerp(0f, _eyelidTargetHeight, eased);

            SetEyelidHeight(_topEyelid, height);
            SetEyelidHeight(_bottomEyelid, height);

            yield return null;
        }

        SetEyelidHeight(_topEyelid, _eyelidTargetHeight);
        SetEyelidHeight(_bottomEyelid, _eyelidTargetHeight);

        if (_holdBeforeQuit > 0f)
            yield return new WaitForSecondsRealtime(_holdBeforeQuit);

        QuitNow();
    }

    private void SetEyelidHeight(RectTransform eyelid, float height)
    {
        var size = eyelid.sizeDelta;
        size.y = height;
        eyelid.sizeDelta = size;
    }

    private void QuitNow()
    {
        Debug.Log("[Exit] Exit Game requested");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}