using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Isaac-style heart display driven entirely by PlayerHealth.OnHealthChanged, with a small
/// "punch" animation on damage and a "pop" animation on healing so the row doesn't feel static.
/// 
/// ANIMATION ARCHITECTURE NOTE: per-heart animations are driven from Update() using tracked
/// elapsed-time floats, NOT coroutines. A coroutine that gets stopped early (StopCoroutine, or
/// the GameObject/component being disabled mid-flight) never runs the code after its final
/// yield — including a "reset scale back to normal" cleanup line — which can leave a heart
/// stuck visually enlarged forever. Update() has no such gap: every frame computes scale
/// directly from elapsed time with no separate cleanup step, and if paused by the GameObject
/// going inactive, it simply resumes from the same elapsed value next frame instead of losing
/// its final reset.
/// 
/// HP UNIT CONVENTION: HealthComponent/PlayerHealth stay plain int, unchanged. This script
/// treats those int units as HALF-HEART units: 2 units = 1 full heart. Even MaxHealth values
/// mean a whole number of hearts (4 = 2 hearts); odd values mean the last heart is a half heart
/// 5 = 2 full hearts + 1 half heart. No other script needs to know about this convention —
/// every existing damage source (EnemyMeleeContactDamage, Projectile, ObstacleType.Damage,
/// PlayerObstacleContactDamage) keeps passing plain int amounts exactly as before.
/// 
/// Heart icons are spawned dynamically from _heartIconPrefab based on MaxHealth, so the row
/// automatically grows/shrinks if MaxHealth ever changes at runtime (e.g. a future max-health
/// pickup) — nothing needs to be hand-placed in the Canvas beyond the container.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    private enum HeartAnimState
    {
        None,
        Punching,
        HealPopping
    }

    [Header("Heart Sprites")]
    [SerializeField] private Sprite _fullHeartSprite;
    [SerializeField] private Sprite _halfHeartSprite;
    [SerializeField] private Sprite _emptyHeartSprite;

    [Header("Layout")]
    [Tooltip("Parent Transform the heart icons are spawned into. Should have a Horizontal Layout Group so icons line up automatically.")]
    [SerializeField] private Transform _heartsContainer;

    [Tooltip("Prefab with an Image component (sprite left unassigned — this script sets it per-instance).")]
    [SerializeField] private GameObject _heartIconPrefab;

    [Header("Damage Punch Animation")]
    [Tooltip("Seconds for the punch-scale-and-flash when a heart takes damage.")]
    [SerializeField] private float _punchDuration = 0.18f;

    [Tooltip("How much bigger the heart scales at the peak of the punch (0.35 = 135% size at peak).")]
    [SerializeField] private float _punchScaleAmount = 0.35f;

    [Tooltip("Color the heart flashes to at the start of a damage punch, then fades back from.")]
    [SerializeField] private Color _damageFlashColor = Color.white;

    [Header("Heal Pop Animation")]
    [Tooltip("Seconds for the scale-in when a heart is restored by healing.")]
    [SerializeField] private float _healPopDuration = 0.25f;

    private readonly List<Image> _heartIcons = new List<Image>();
    private readonly List<HeartAnimState> _heartAnimStates = new List<HeartAnimState>();
    private readonly List<float> _heartAnimElapsed = new List<float>();

    private int _lastMaxUnits = -1;
    private int _previousCurrentUnits = -1;
    private bool _hasReceivedFirstUpdate;
    private Coroutine _subscribeRoutine;

    private void OnEnable()
    {
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
        {
            PlayerHealth.Instance.OnHealthChanged -= HandleHealthChanged;
        }

        // Defensive: snap every heart back to resting scale/color immediately on disable,
        // so if this component (or its GameObject) gets toggled off mid-animation for any
        // reason, it never comes back showing a heart frozen mid-punch.
        SnapAllHeartsToResting();
    }

    /// <summary>
    /// PlayerHealth.Instance may not exist yet the instant this UI enables (scene load
    /// order isn't guaranteed) — poll once per frame until it appears, same problem
    /// MinimapController/PlayerAnimator solve by reading *.Instance defensively.
    /// </summary>
    private IEnumerator SubscribeWhenReady()
    {
        while (PlayerHealth.Instance == null)
        {
            yield return null;
        }

        PlayerHealth.Instance.OnHealthChanged += HandleHealthChanged;

        HandleHealthChanged(
            PlayerHealth.Instance.CurrentHealth,
            PlayerHealth.Instance.MaxHealth
        );

        _subscribeRoutine = null;
    }

    private void Update()
    {
        for (int i = 0; i < _heartAnimStates.Count; i++)
        {
            if (_heartAnimStates[i] == HeartAnimState.None)
            {
                continue;
            }

            if (i >= _heartIcons.Count || _heartIcons[i] == null)
            {
                _heartAnimStates[i] = HeartAnimState.None;
                continue;
            }

            _heartAnimElapsed[i] += Time.deltaTime;

            float duration =
                _heartAnimStates[i] == HeartAnimState.Punching
                    ? Mathf.Max(0.01f, _punchDuration)
                    : Mathf.Max(0.01f, _healPopDuration);

            float t = Mathf.Clamp01(_heartAnimElapsed[i] / duration);

            RectTransform rt = _heartIcons[i].rectTransform;

            if (_heartAnimStates[i] == HeartAnimState.Punching)
            {
                // Sin(0..PI) rises to a peak at t=0.5 and returns to 0 at t=1 —
                // a clean punch-out-and-settle in one continuous curve.
                float punch =
                    Mathf.Sin(t * Mathf.PI) * _punchScaleAmount;

                rt.localScale = Vector3.one * (1f + punch);

                _heartIcons[i].color =
                    Color.Lerp(_damageFlashColor, Color.white, t);
            }
            else // HealPopping
            {
                rt.localScale = Vector3.one * EaseOutBack(t);
            }

            if (t >= 1f)
            {
                rt.localScale = Vector3.one;
                _heartIcons[i].color = Color.white;
                _heartAnimStates[i] = HeartAnimState.None;
            }
        }
    }

    private void HandleHealthChanged(int currentUnits, int maxUnits)
    {
        bool maxChanged = maxUnits != _lastMaxUnits;

        if (maxChanged)
        {
            RebuildHeartIcons(maxUnits);
            _lastMaxUnits = maxUnits;
        }

        // Skip animation on the very first update (initial subscribe) and on any frame
        // the heart row was just rebuilt — there's no meaningful "previous" state to
        // punch/pop from.
        bool skipAnimation =
            !_hasReceivedFirstUpdate || maxChanged;

        UpdateHeartSprites(currentUnits, skipAnimation);

        _previousCurrentUnits = currentUnits;
        _hasReceivedFirstUpdate = true;
    }

    private void RebuildHeartIcons(int maxUnits)
    {
        foreach (var icon in _heartIcons)
        {
            if (icon != null)
            {
                Destroy(icon.gameObject);
            }
        }

        _heartIcons.Clear();
        _heartAnimStates.Clear();
        _heartAnimElapsed.Clear();

        if (_heartsContainer == null || _heartIconPrefab == null)
        {
            Debug.LogWarning(
                "[PlayerHealthUI] Hearts Container or Heart Icon Prefab not assigned — can't build the heart row."
            );

            return;
        }

        int heartCount = Mathf.CeilToInt(maxUnits / 2f);

        for (int i = 0; i < heartCount; i++)
        {
            GameObject instance =
                Instantiate(_heartIconPrefab, _heartsContainer);

            instance.name = $"Heart_{i}";

            var image = instance.GetComponent<Image>();

            if (image == null)
            {
                Debug.LogWarning(
                    "[PlayerHealthUI] Heart Icon Prefab has no Image component — skipping."
                );

                Destroy(instance);
                continue;
            }

            image.rectTransform.localScale = Vector3.one;
            image.color = Color.white;

            _heartIcons.Add(image);
            _heartAnimStates.Add(HeartAnimState.None);
            _heartAnimElapsed.Add(0f);
        }
    }

    private void UpdateHeartSprites(int currentUnits, bool skipAnimation)
    {
        for (int i = 0; i < _heartIcons.Count; i++)
        {
            int newUnitsForHeart =
                Mathf.Clamp(currentUnits - (i * 2), 0, 2);

            Sprite sprite =
                newUnitsForHeart >= 2
                    ? _fullHeartSprite
                    : newUnitsForHeart == 1
                        ? _halfHeartSprite
                        : _emptyHeartSprite;

            if (sprite == null)
            {
                Debug.LogWarning(
                    $"[PlayerHealthUI] Missing a heart sprite assignment (full/half/empty) — heart {i} won't render correctly."
                );
            }

            _heartIcons[i].sprite = sprite;
            _heartIcons[i].enabled = sprite != null;

            if (skipAnimation)
            {
                continue;
            }

            int oldUnitsForHeart =
                Mathf.Clamp(_previousCurrentUnits - (i * 2), 0, 2);

            if (newUnitsForHeart < oldUnitsForHeart)
            {
                StartHeartAnimation(i, HeartAnimState.Punching);
            }
            else if (newUnitsForHeart > oldUnitsForHeart)
            {
                StartHeartAnimation(i, HeartAnimState.HealPopping);
            }
        }
    }

    private void StartHeartAnimation(
        int heartIndex,
        HeartAnimState state)
    {
        if (heartIndex < 0 ||
            heartIndex >= _heartAnimStates.Count)
        {
            return;
        }

        _heartAnimStates[heartIndex] = state;
        _heartAnimElapsed[heartIndex] = 0f;
    }

    private void SnapAllHeartsToResting()
    {
        for (int i = 0; i < _heartIcons.Count; i++)
        {
            if (_heartIcons[i] == null)
            {
                continue;
            }

            _heartIcons[i].rectTransform.localScale = Vector3.one;
            _heartIcons[i].color = Color.white;
        }

        for (int i = 0; i < _heartAnimStates.Count; i++)
        {
            _heartAnimStates[i] = HeartAnimState.None;
        }
    }

    /// <summary>
    /// Standard "ease out back" curve: overshoots past 1 before settling — gives the
    /// heal pop a bit of springiness instead of a flat linear scale-in.
    /// </summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float x = t - 1f;

        return 1f +
               c3 * (x * x * x) +
               c1 * (x * x);
    }
}