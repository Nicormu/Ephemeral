using System.Collections;
using UnityEngine;

/// <summary>
/// Flickers this entity's sprite (alternating alpha) for the duration of its post-damage
/// invulnerability window, giving clear "I just got hit and can't be hit again yet" feedback.
/// Works on both the player and enemies — subscribes to HealthComponent.OnDamaged, which fires
/// only when a hit actually applies (mirrors the same guard HealthComponent.TakeDamage already
/// uses), so this never flickers on a no-op hit (already invulnerable, dead, or 0 damage).
///
/// Reads HealthComponent.InvulnerabilityDuration directly rather than a separately-tuned value,
/// so the flicker always exactly covers the window during which the entity genuinely can't take
/// another hit — no risk of the visual ending before (or lingering after) actual invulnerability.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class HitFlashEffect : MonoBehaviour
{
    [Tooltip("SpriteRenderer to flicker. Leave empty to auto-find — checks this GameObject first, then searches children (needed for enemies that split physics/root from an animated visual child, e.g. the bat rig).")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Tooltip("Seconds between each visibility toggle. Smaller = faster flicker.")]
    [SerializeField] private float _blinkInterval = 0.05f;

    [Tooltip("Alpha shown during the 'dim' half of each blink cycle. 0 = fully invisible blink, higher = a softer flicker.")]
    [Range(0f, 1f)]
    [SerializeField] private float _dimAlpha = 0.3f;

    private HealthComponent _health;
    private Coroutine _flickerRoutine;
    private float _restingAlpha = 1f;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();

        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (_spriteRenderer == null)
            Debug.LogWarning($"[HitFlashEffect] '{name}' couldn't find a SpriteRenderer on itself or its children — no flicker will play.");
    }

    private void OnEnable()
    {
        _health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        _health.OnDamaged -= HandleDamaged;
        StopFlicker();
    }

    private void HandleDamaged(int amount)
    {
        if (_spriteRenderer == null) return;

        if (_flickerRoutine != null) StopCoroutine(_flickerRoutine);
        _flickerRoutine = StartCoroutine(Flicker(_health.InvulnerabilityDuration));
    }

    private IEnumerator Flicker(float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        _restingAlpha = _spriteRenderer.color.a;

        float elapsed = 0f;
        bool dim = false;

        while (elapsed < duration)
        {
            SetAlpha(dim ? _dimAlpha : _restingAlpha);
            dim = !dim;

            float wait = Mathf.Min(_blinkInterval, duration - elapsed);
            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }

        SetAlpha(_restingAlpha);
        _flickerRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        Color c = _spriteRenderer.color;
        c.a = alpha;
        _spriteRenderer.color = c;
    }

    private void StopFlicker()
    {
        if (_flickerRoutine != null)
        {
            StopCoroutine(_flickerRoutine);
            _flickerRoutine = null;
        }

        if (_spriteRenderer != null)
            SetAlpha(_restingAlpha);
    }
}