using UnityEngine;

/// <summary>
/// Player health - wraps HealthComponent and exposes singleton access + healing.
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
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[PlayerHealth] Another instance of PlayerHealth already exists. Destroying this one.");
            Destroy(this);
            return;
        }

        Instance = this;
        _health = GetComponent<HealthComponent>();
    }

        /// <summary>
    /// Singleton cleanup lives in OnDestroy, not OnDisable — mirrors PlayerMovement.Instance's
    /// same fix. If anything ever disables this component instead of destroying it (e.g. a
    /// future "invulnerable while disabled" trick), Instance must stay valid so every system
    /// reading PlayerHealth.Instance (UI, hazard detector, death screen, weapons...) keeps
    /// working. Only Awake() sets Instance, so there's no re-registration path on re-enable —
    /// nulling it in OnDisable would leave it permanently null until re-enabled.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Restore health. Unlike most entities, the player can heal.
    /// </summary>
    public void Heal(int amount) => _health.Heal(amount);

    /// <summary>
    /// Apply damage through the underlying HealthComponent.
    /// </summary>
    public void TakeDamage(int amount) => _health.TakeDamage(amount);

    /// <summary>
    /// Restores the player to full health and clears IsDead — used for a full run reset (dungeon regenerate + respawn).
    /// </summary>
    public void ResetHealth() => _health.ResetHealth();
}