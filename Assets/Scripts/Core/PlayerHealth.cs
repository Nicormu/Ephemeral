using UnityEngine;

/// <summary>
/// Player health — wraps HealthComponent and exposes singleton access + healing.
/// The HealthComponent is attached to the same GameObject; PlayerHealth delegates to it
/// and patches its default values from this class's inspector fields.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Health")]
    [SerializeField] private int _maxHealth = 6;

    [Tooltip("Invulnerability window after taking damage to prevent rapid re-trigger.")]
    [SerializeField] private float _invulnerabilityDuration = 1f;

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

        // Patch base defaults from inspector values on this wrapper.
        _health.Configure(_maxHealth, _invulnerabilityDuration);
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
