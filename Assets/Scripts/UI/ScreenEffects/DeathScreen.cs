using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Listens for PlayerHealth.OnDied. On death: locks player input, fades to black via
/// ScreenFader, then reveals "Press any key to continue" text with a gentle looping scale pulse
/// so it reads as interactive rather than static. Once any key is pressed, it stops the pulse,
/// hides the text and hands off to DungeonResetController.ResetRun() — which regenerates the
/// dungeon, resets player health/position, and fades back out itself (it detects the screen is
/// already black and skips its own fade-in, so there's no double-fade).
/// </summary>
public class PlayerDeathScreen : MonoBehaviour
{
    [Header("References")]
    [Tooltip("'Press any key to continue' text. Starts hidden — this script activates it once the fade-in finishes.")]
    [SerializeField] private TextMeshProUGUI _continueText;

    [Header("Timing")]
    [Tooltip("Seconds for the fade-in to black on death. Uses the scene's ScreenFader.")]
    [SerializeField] private float _fadeDuration = 1f;

    [Tooltip("Extra pause after the fade-in completes before 'any key' starts being listened for — avoids the same keypress that caused death (or a held key) instantly skipping the screen.")]
    [SerializeField] private float _inputDelayAfterFade = 0.3f;

    [Header("Continue Text Pulse")]
    [Tooltip("How much bigger the text grows at the peak of the pulse (0.08 = 108% size at peak).")]
    [SerializeField] private float _pulseScaleAmount = 0.08f;

    [Tooltip("Seconds for one full grow-and-shrink cycle.")]
    [SerializeField] private float _pulseCycleDuration = 1.2f;

    private bool _isDeathSequenceActive;
    private Coroutine _subscribeRoutine;
    private Coroutine _pulseRoutine;

    private void OnEnable()
    {
        if (_continueText != null) _continueText.gameObject.SetActive(false);
        _subscribeRoutine = StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        if (_subscribeRoutine != null)
        {
            StopCoroutine(_subscribeRoutine);
            _subscribeRoutine = null;
        }

        StopContinueTextPulse();

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.OnDied -= HandlePlayerDied;
    }

    /// <summary>PlayerHealth.Instance may not exist yet the instant this enables (scene load
    /// order isn't guaranteed) — same pattern PlayerHealthUI already uses.</summary>
    private IEnumerator SubscribeWhenReady()
    {
        while (PlayerHealth.Instance == null)
            yield return null;

        PlayerHealth.Instance.OnDied += HandlePlayerDied;
        _subscribeRoutine = null;
    }

    private void HandlePlayerDied()
    {
        if (_isDeathSequenceActive) return;
        StartCoroutine(RunDeathSequence());
    }

    private IEnumerator RunDeathSequence()
    {
        _isDeathSequenceActive = true;

        if (PlayerMovement.Instance != null)
            PlayerMovement.Instance.SetInputEnabled(false);

        if (ScreenFader.Instance != null)
            yield return StartCoroutine(ScreenFader.Instance.FadeToBlack(_fadeDuration));
        else
            Debug.LogWarning("[PlayerDeathScreen] No ScreenFader in the scene — screen won't fade to black. Add one (see ScreenFader.cs).");

        if (_continueText != null)
        {
            _continueText.gameObject.SetActive(true);
            StartContinueTextPulse();
        }

        // Unscaled so this still works if Time.timeScale is ever paused on death.
        yield return new WaitForSecondsRealtime(_inputDelayAfterFade);

        while (!Input.anyKeyDown)
            yield return null;

        StopContinueTextPulse();
        if (_continueText != null) _continueText.gameObject.SetActive(false);

        if (DungeonResetController.Instance != null)
            DungeonResetController.Instance.ResetRun(); // screen is already black — it skips straight to the reset, then fades back out and re-enables input itself
        else
            Debug.LogWarning("[PlayerDeathScreen] No DungeonResetController in the scene — can't reset the run. Add one (see DungeonResetController.cs).");

        _isDeathSequenceActive = false;
    }

    /// <summary>Starts (or restarts) the looping scale pulse on _continueText. Safe to call
    /// repeatedly — stops any existing pulse first so it never stacks two coroutines driving the
    /// same RectTransform.</summary>
    private void StartContinueTextPulse()
    {
        StopContinueTextPulse();
        _pulseRoutine = StartCoroutine(PulseContinueText());
    }

    private void StopContinueTextPulse()
    {
        if (_pulseRoutine != null)
        {
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
        }

        // Always snap back to resting scale so a stopped pulse never leaves the text stuck
        // mid-grow/shrink the next time it's shown.
        if (_continueText != null)
            _continueText.rectTransform.localScale = Vector3.one;
    }

    /// <summary>Continuous grow-and-shrink loop using a sine wave for a smooth, gentle pulse —
    /// runs on unscaled time so it keeps animating regardless of Time.timeScale on the death
    /// screen. Loops forever until StopContinueTextPulse() cancels it.</summary>
    private IEnumerator PulseContinueText()
    {
        RectTransform rt = _continueText.rectTransform;
        float duration = Mathf.Max(0.01f, _pulseCycleDuration);
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            // Sine wave from 0 -> 1 -> 0 over one full cycle, so the scale eases smoothly in
            // both directions instead of snapping at the peak.
            float t = (elapsed % duration) / duration;
            float pulse = Mathf.Sin(t * Mathf.PI * 2f) * _pulseScaleAmount;

            rt.localScale = Vector3.one * (1f + pulse);

            yield return null;
        }
    }
}