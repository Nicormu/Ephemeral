using System.Collections;
using UnityEngine;

/// <summary>
/// Central "restart the run" entry point. Regenerates the dungeon (new seed via
/// DungeonManager.Regenerate, which also repositions the player at the new Start room) and
/// restores the player's health to full — wrapped in a fade-to-black / fade-back-in via
/// ScreenFader so it isn't a jarring instant cut, with no text (that's PlayerDeathScreen's job).
///
/// Called two ways:
///   1. Directly by the player pressing R at any time during free-roam (see Update()) — fades to
///      black, resets, fades back in, all on its own.
///   2. By PlayerDeathScreen once the player presses any key to continue after dying. In that
///      case the screen is ALREADY black (PlayerDeathScreen faded it in itself) — ScreenFader.
///      IsBlack lets this skip a redundant second fade-in and go straight to resetting, then just
///      fades back out.
/// </summary>
public class DungeonResetController : MonoBehaviour
{
    public static DungeonResetController Instance { get; private set; }

    [Tooltip("Key that regenerates the dungeon and resets the player during free-roam.")]
    [SerializeField] private KeyCode _resetKey = KeyCode.R;

    [Tooltip("Seconds for the fade-to-black / fade-back-in around a reset. Only applies if a ScreenFader is present in the scene — without one, the reset just happens instantly with no fade.")]
    [SerializeField] private float _fadeDuration = 0.5f;

    private bool _isResetting;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(_resetKey))
            ResetRun();
    }

    /// <summary>Regenerates the dungeon and restores the player to full health/position, fading
    /// through black. Ignored if a reset is already in progress. Safe to call from anywhere.</summary>
    public void ResetRun()
    {
        if (_isResetting) return;
        StartCoroutine(ResetRunSequence());
    }

    private IEnumerator ResetRunSequence()
    {
        _isResetting = true;

        if (PlayerMovement.Instance != null)
            PlayerMovement.Instance.SetInputEnabled(false);

        bool alreadyBlack = ScreenFader.Instance != null && ScreenFader.Instance.IsBlack;

        if (ScreenFader.Instance != null && !alreadyBlack)
            yield return StartCoroutine(ScreenFader.Instance.FadeToBlack(_fadeDuration));

        PerformReset();

        if (ScreenFader.Instance != null)
            yield return StartCoroutine(ScreenFader.Instance.FadeFromBlack(_fadeDuration));

        if (PlayerMovement.Instance != null)
            PlayerMovement.Instance.SetInputEnabled(true);

        _isResetting = false;
    }

    private void PerformReset()
    {
        if (DungeonManager.Instance != null)
            DungeonManager.Instance.Regenerate();

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.ResetHealth();
    }
}