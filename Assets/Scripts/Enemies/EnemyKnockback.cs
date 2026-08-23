using System.Collections;
using UnityEngine;

/// <summary>
/// Applies a brief knockback impulse to this enemy when it takes damage, pushing it directly
/// away from the damage source. Temporarily disables EnemyChaseMovement (if present) for the
/// duration of the knockback — without that, EnemyChaseMovement's own FixedUpdate would
/// immediately overwrite the knockback velocity on the very next physics step, since it sets
/// rb.linearVelocity toward the player every frame.
///
/// Add this to any enemy prefab that should get knocked back on hit. Stationary enemies with no
/// EnemyChaseMovement (e.g. a turret using only EnemyRangedAttack) still get pushed — there's
/// just nothing to suppress.
///
/// Triggered via EnemyHealth.TakeDamage(amount, sourcePosition) — see that method's doc.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyKnockback : MonoBehaviour
{
    [Tooltip("How resistant this enemy is to knockback. Actual pushback distance is the incoming weapon's Knockback Power divided by this value — e.g. Weight 1 vs a weapon with Knockback Power 6 travels twice as far as Weight 2 hit by the same weapon. Must stay above 0.")]
    [SerializeField] private float _weight = 1f;

    [Tooltip("Seconds the knockback lasts — also how long EnemyChaseMovement is disabled so it can't fight the pushback. Duration does NOT scale with weight, only distance does. The velocity eases down to zero over this duration rather than stopping abruptly.")]
    [SerializeField] private float _knockbackDuration = 0.15f;

    private Rigidbody2D _rb;
    private EnemyChaseMovement _chaseMovement; // optional — null for stationary enemies

    private Coroutine _knockbackRoutine;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _chaseMovement = GetComponent<EnemyChaseMovement>();
    }

    /// <summary>Pushes this enemy directly away from sourcePosition. knockbackPower comes from
    /// whatever dealt the hit (see PlayerWeapon) — the actual speed applied is
    /// knockbackPower / this enemy's own Weight, so a heavy enemy travels a shorter distance from
    /// the exact same weapon hit than a light one. Safe to call again mid-knockback — a new hit
    /// restarts the knockback from scratch instead of stacking/adding.</summary>
    public void ApplyKnockback(Vector2 sourcePosition, float knockbackPower)
    {
        Vector2 direction = (Vector2)transform.position - sourcePosition;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Random.insideUnitCircle.normalized;

        float effectiveForce = knockbackPower / Mathf.Max(0.01f, _weight);

        if (_knockbackRoutine != null) StopCoroutine(_knockbackRoutine);
        _knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, effectiveForce));
    }

    private IEnumerator KnockbackRoutine(Vector2 direction, float force)
    {
        if (_chaseMovement != null) _chaseMovement.enabled = false;

        float duration = Mathf.Max(0.01f, _knockbackDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = elapsed / duration;
            _rb.linearVelocity = Vector2.Lerp(direction * force, Vector2.zero, t);
            yield return new WaitForFixedUpdate();
        }

        _rb.linearVelocity = Vector2.zero;

        if (_chaseMovement != null) _chaseMovement.enabled = true;

        _knockbackRoutine = null;
    }
}