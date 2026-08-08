using UnityEngine;

/// <summary>
/// Player health — wraps HealthComponent and exposes singleton access + healing.
/// Max Health / Starting Health / Invulnerability are configured on the HealthComponent's own
/// Inspector fields now (same GameObject) — this wrapper no longer duplicates or patches them.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    private HealthComponent _health;

    public int CurrentHealth => _health.CurrentHealth;
    public int MaxHealth => _health.MaxHealth;
    public bool IsInvulnerable => _health.IsInvulnerable;
    public bool IsDead => _health.IsDead;

    public event System.Action<int, int> OnHealthChanged { add => _health.OnHealthChanged += value; remove => _health.OnHealthChanged -= value; }
    public event System.Action OnDied { add => _health.OnDied += value; remove => _health.OnDied -= value; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _health = GetComponent<HealthComponent>();
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Restore health. Unlike most entities, the player can heal.</summary>
    public void Heal(int amount) => _health.Heal(amount);

    /// <summary>Apply damage through the underlying HealthComponent.</summary>
    public void TakeDamage(int amount) => _health.TakeDamage(amount);
}