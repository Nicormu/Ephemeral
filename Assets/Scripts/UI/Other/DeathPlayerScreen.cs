using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Listens for PlayerHealth.OnDied. On death: locks player input, fades to black via
/// ScreenFader, then reveals "Press any key to continue" text. Once any key is pressed, it hides
/// the text and hands off to DungeonResetController.ResetRun() — which regenerates the dungeon,
/// resets player health/position, and fades back out itself (it detects the screen is already
/// black and skips its own fade-in, so there's no double-fade).
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

    private bool _isDeathSequenceActive;
    private Coroutine _subscribeRoutine;

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

        if (_continueText != null) _continueText.gameObject.SetActive(true);

        // Unscaled so this still works if Time.timeScale is ever paused on death.
        yield return new WaitForSecondsRealtime(_inputDelayAfterFade);

        while (!Input.anyKeyDown)
            yield return null;

        if (_continueText != null) _continueText.gameObject.SetActive(false);

        if (DungeonResetController.Instance != null)
            DungeonResetController.Instance.ResetRun(); // screen is already black — it skips straight to the reset, then fades back out and re-enables input itself
        else
            Debug.LogWarning("[PlayerDeathScreen] No DungeonResetController in the scene — can't reset the run. Add one (see DungeonResetController.cs).");

        _isDeathSequenceActive = false;
    }
}