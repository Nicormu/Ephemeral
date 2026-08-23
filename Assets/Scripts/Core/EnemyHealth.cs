using UnityEngine;

/// <summary>
/// Enemy health — wraps HealthComponent and destroys the GameObject on death so
/// RoomController can detect it and mark the room cleared.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class EnemyHealth : MonoBehaviour
{
    private HealthComponent _health;
    private EnemyKnockback _knockback; // optional — enemies with no EnemyKnockback just never get pushed

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
        _health.OnDied += DestroySelf; // auto-destroy when health hits zero
    }

    private void OnDisable()
    {
        _health.OnDied -= DestroySelf; // avoid dangling reference
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

    private void DestroySelf() => Destroy(gameObject);
}