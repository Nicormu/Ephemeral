using UnityEngine;

/// <summary>
/// Enemy health — wraps HealthComponent and destroys the GameObject on death so
/// RoomController can detect it and mark the room cleared.
///
/// BUG FIX: death used to destroy the GameObject the instant HealthComponent.OnDied fired,
/// which meant EnemyAnimator.PlayDeath() was never called and the Die animation never had a
/// chance to play — the enemy just vanished. Death is now animation-gated: HandleDied() plays
/// the Die animation via EnemyAnimator (if present) and only destroys the GameObject once
/// EnemyAnimator.OnDeathAnimationFinished fires — which happens either from the clip's own
/// Animation Event or its fallback timer if that event isn't wired up on a given prefab. Enemies
/// with no EnemyAnimator component (no death art configured yet) fall back to the old
/// instant-destroy behavior, so nothing breaks for enemies that don't have one.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class EnemyHealth : MonoBehaviour
{
    private HealthComponent _health;
    private EnemyKnockback _knockback; // optional — enemies with no EnemyKnockback just never get pushed
    private EnemyAnimator _enemyAnimator; // optional — enemies with no death art just instant-destroy

    public int CurrentHealth => _health.CurrentHealth;
    public int MaxHealth => _health.MaxHealth;
    public bool IsInvulnerable => _health.IsInvulnerable;
    public bool IsDead => _health.IsDead;

    public event System.Action<int, int> OnHealthChanged { add => _health.OnHealthChanged += value; remove => _health.OnHealthChanged -= value; }
    public event System.Action OnDied { add => _health.OnDied += value; remove => _health.OnDied -= value; }

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
        _knockback = GetComponent<EnemyKnockback>();
        _enemyAnimator = GetComponent<EnemyAnimator>();

        _health.OnDied += HandleDied;

        if (_enemyAnimator != null)
            _enemyAnimator.OnDeathAnimationFinished += DestroySelf;
    }

    private void OnDisable()
    {
        _health.OnDied -= HandleDied;

        if (_enemyAnimator != null)
            _enemyAnimator.OnDeathAnimationFinished -= DestroySelf;
    }

    public void TakeDamage(int amount, Vector2? sourcePosition = null, float knockbackPower = 0f)
    {
        // Snapshot whether this hit will actually land BEFORE calling TakeDamage — mirrors
        // HealthComponent.TakeDamage's own guard (IsDead || IsInvulnerable || amount <= 0), so
        // knockback only fires on a hit that actually applied damage, not a no-op one.
        bool willApply = !_health.IsDead && !_health.IsInvulnerable && amount > 0;

        _health.TakeDamage(amount);

        if (willApply && sourcePosition.HasValue && knockbackPower > 0f && _knockback != null)
            _knockback.ApplyKnockback(sourcePosition.Value, knockbackPower);
    }

    /// <summary>Fired by HealthComponent.OnDied. Hands off to EnemyAnimator to play the death
    /// clip if one exists — DestroySelf() is deferred until OnDeathAnimationFinished fires.</summary>
    private void HandleDied()
    {
        if (_enemyAnimator != null)
            _enemyAnimator.PlayDeath();
        else
            DestroySelf(); // no death art configured on this enemy — keep the old instant-destroy behavior
    }

    private void DestroySelf() => Destroy(gameObject);
}