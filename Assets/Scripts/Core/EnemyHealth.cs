using UnityEngine;

/// <summary>
/// Enemy health — wraps HealthComponent and destroys the GameObject on death so RoomController
/// can detect it (enemy cleanup depends on Destroy()). Max Health / Starting Health /
/// Invulnerability are configured on the HealthComponent's own Inspector fields now (same
/// GameObject) — this wrapper no longer duplicates or patches them.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class EnemyHealth : MonoBehaviour
{
    private HealthComponent _health;

    public int CurrentHealth => _health.CurrentHealth;
    public int MaxHealth => _health.MaxHealth;
    public bool IsInvulnerable => _health.IsInvulnerable;
    public bool IsDead => _health.IsDead;

    public event System.Action<int, int> OnHealthChanged { add => _health.OnHealthChanged += value; remove => _health.OnHealthChanged -= value; }
    public event System.Action OnDied { add => _health.OnDied += value; remove => _health.OnDied -= value; }

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
        _health.OnDied += DestroySelf; // auto-destroy when health hits zero
    }

    private void OnDisable()
    {
        _health.OnDied -= DestroySelf; // avoid dangling reference
    }

    private void DestroySelf() => Destroy(gameObject);
}