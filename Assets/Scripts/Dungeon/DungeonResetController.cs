using System.Collections;
using UnityEngine;

/// <summary>
/// Central "restart the run" entry point. Regenerates the dungeon (new seed via
/// DungeonManager.Regenerate, which also repositions the player at the new Start room) and
/// restores the player's health to full.
///
/// Two ways a reset can happen:
///
///   1. HOLD-TO-RESET (free-roam): holding _resetKey fades the screen to black over
///      _holdDuration seconds; releasing early cancels and fades back. Completing a hold performs
///      the reset while the screen is fully black, then — if the key is STILL held — immediately
///      starts counting the next hold cycle from black (no visible flash between resets). Every
///      hold cycle completed without ever releasing the key counts toward
///      _restartsBeforeFastHold; once that many have happened in the same continuous hold, every
///      further cycle in that hold uses the shorter _fastHoldDuration instead. Releasing the key
///      at any point resets that streak back to _holdDuration for the next hold.
///
///      Deliberately does NOT touch PlayerMovement.SetInputEnabled — the player stays fully in
///      control the whole time the screen is fading, so holding/releasing R to bail never feels
///      like it locked them out of anything. PerformReset() itself is synchronous (no yield
///      inside it), so there's no frame where movement could interfere mid-regenerate anyway.
///
///   2. INSTANT (ResetRun(), public): called by PlayerDeathScreen once the player presses any key
///      to continue after dying. The screen is ALREADY black in that case (PlayerDeathScreen
///      faded it in itself) — ScreenFader.IsBlack lets this skip a redundant fade-in and go
///      straight to resetting, then just fades back out. Left completely separate from the hold
///      logic so death always resets on a single press with no waiting. This path still disables
///      input during the reset — the player is already locked out on the death screen at that
///      point anyway (see PlayerDeathScreen.RunDeathSequence), so this just keeps that state
///      consistent through to the reset finishing.
/// </summary>
public class DungeonResetController : MonoBehaviour
{
    public static DungeonResetController Instance { get; private set; }

    [Tooltip("Key that regenerates the dungeon and resets the player during free-roam.")]
    [SerializeField] private KeyCode _resetKey = KeyCode.R;

    [Tooltip("Seconds for the fade-to-black / fade-back-in around an instant reset (ResetRun(), e.g. from the death screen), and for fading back to clear if a hold is released early. Only applies if a ScreenFader is present in the scene — without one, resets happen instantly with no fade.")]
    [SerializeField] private float _fadeDuration = 0.5f;

    [Header("Hold-to-Reset")]
    [Tooltip("Seconds _resetKey must be held down (from a full release) before a reset triggers.")]
    [SerializeField] private float _holdDuration = 5f;

    [Tooltip("Once this many resets have happened in a row WITHOUT ever releasing _resetKey, every further hold cycle in that same hold uses _fastHoldDuration instead of _holdDuration.")]
    [SerializeField] private int _restartsBeforeFastHold = 3;

    [Tooltip("Shorter hold time used once _restartsBeforeFastHold consecutive resets have happened in the same continuous hold.")]
    [SerializeField] private float _fastHoldDuration = 2f;

    private bool _isResetting;
    private Coroutine _holdRoutine;
    private int _consecutiveHoldRestarts;

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
        if (Input.GetKey(_resetKey))
        {
            if (_holdRoutine == null && !_isResetting)
                _holdRoutine = StartCoroutine(HoldToResetRoutine());
        }
    }

    /// <summary>Regenerates the dungeon and restores the player to full health/position, fading
    /// through black. Ignored if a reset is already in progress (instant or hold-driven). Safe to
    /// call from anywhere — this is the path PlayerDeathScreen uses, deliberately independent of
    /// the hold-to-reset key logic below so death always resets on a single keypress.</summary>
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

    /// <summary>Drives the hold-to-restart key. Loops entire hold cycles (fade-to-black over the
    /// current threshold -> reset -> possibly loop again) for as long as _resetKey stays held,
    /// speeding up after _restartsBeforeFastHold consecutive completions. Exits (fading back to
    /// clear if needed) the moment the key is released. Player movement is left fully enabled
    /// throughout — see class doc.</summary>
    private IEnumerator HoldToResetRoutine()
    {
        _isResetting = true;

        while (Input.GetKey(_resetKey))
        {
            float threshold = _consecutiveHoldRestarts >= _restartsBeforeFastHold ? _fastHoldDuration : _holdDuration;
            threshold = Mathf.Max(0.01f, threshold);

            float elapsed = 0f;
            bool completedThisCycle = false;

            while (Input.GetKey(_resetKey))
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / threshold);

                if (ScreenFader.Instance != null)
                    ScreenFader.Instance.SetAlpha(t);

                if (t >= 1f)
                {
                    completedThisCycle = true;
                    break;
                }

                yield return null;
            }

            if (!completedThisCycle)
            {
                // Released before this hold cycle finished — cancel, fade back to clear, and
                // reset the speed-up streak for next time.
                if (ScreenFader.Instance != null)
                    yield return StartCoroutine(ScreenFader.Instance.FadeFromBlack(_fadeDuration));

                _consecutiveHoldRestarts = 0;
                break;
            }

            // Full hold completed while the screen is fully black — reset right here, hidden.
            PerformReset();
            _consecutiveHoldRestarts++;

            // Loop back: if still held, immediately start the next (possibly faster) cycle from
            // black — no visible flash between back-to-back resets.
        }

        // Key released (or the loop above already handled it) — fade back out if we ended on
        // black (i.e. the last thing that happened was a completed reset, not an early release,
        // which already fades itself back out above).
        if (ScreenFader.Instance != null && ScreenFader.Instance.IsBlack)
            yield return StartCoroutine(ScreenFader.Instance.FadeFromBlack(_fadeDuration));

        _holdRoutine = null;
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