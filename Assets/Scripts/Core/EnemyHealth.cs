using UnityEngine;

/// <summary>
/// Enemy health — wraps HealthComponent and destroys the GameObject on death
/// so RoomController can detect it (enemy cleanup depends on Destroy()).
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int _maxHealth = 3;

    [Tooltip("Brief invulnerability after taking damage, so multiple hits in one frame/overlap don't stack.")]
    [SerializeField] private float _invulnerabilityDuration = 0.1f;

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
        _health.Configure(_maxHealth, _invulnerabilityDuration);
        _health.OnDied += DestroySelf; // auto-destroy when health hits zero
    }

    private void OnDisable()
    {
        _health.OnDied -= DestroySelf; // avoid dangling reference
    }

    private void DestroySelf() => Destroy(gameObject);
}
