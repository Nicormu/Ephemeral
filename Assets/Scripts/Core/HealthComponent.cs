using System;
using UnityEngine;

/// <summary>
/// Shared health logic for any entity that can take damage and die.
/// Max Health, Starting Health, and Invulnerability Duration are configured directly here in
/// the Inspector — this is now the ONLY place these values live. PlayerHealth / EnemyHealth no
/// longer duplicate them; they just wrap this component's public API (TakeDamage, Heal, events)
/// for singleton access and entity-specific behavior (e.g. auto-destroy on death for enemies).
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int _maxHealth = 3;

    [Tooltip("HP this entity starts with. Can be lower than Max Health (e.g. an enemy that spawns already damaged). Set equal to Max Health for a normal full-health start.")]
    [SerializeField] private int _startingHealth = 3;

    [Tooltip("Brief invulnerability after taking damage so multiple hits in one frame don't stack.")]
    [SerializeField] private float _invulnerabilityDuration = 0.1f;

    private int _currentHealth;
    private float _lastDamageTime = -999f;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool IsInvulnerable => Time.time - _lastDamageTime < _invulnerabilityDuration;
    public bool IsDead => _currentHealth <= 0;

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDied;

    private void Awake()
    {
        _currentHealth = Mathf.Clamp(_startingHealth, 0, _maxHealth);
    }

    /// <summary>
    /// Reconfigure health values at runtime (e.g. difficulty scaling, or spawning a "damaged"
    /// enemy variant from code). Unlike the old Configure(), this explicitly RESETS current
    /// health to newStartingHealth instead of just clamping whatever was left over — so the
    /// result no longer depends on component Awake() execution order.
    /// </summary>
    public void Configure(int newMaxHealth, int newStartingHealth, float newInvulnerabilityDuration)
    {
        _maxHealth = Mathf.Max(1, newMaxHealth);
        _startingHealth = Mathf.Clamp(newStartingHealth, 0, _maxHealth);
        _currentHealth = _startingHealth;
        _invulnerabilityDuration = newInvulnerabilityDuration;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    /// <summary>Apply damage with invulnerability guard. Raises OnHealthChanged and possibly OnDied.</summary>
    public void TakeDamage(int amount)
    {
        if (IsDead || IsInvulnerable || amount <= 0) return;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        _lastDamageTime = Time.time;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
            Die();
    }

    /// <summary>Heal up to Max Health. Override in a subclass to disable healing for specific entity types.</summary>
    public virtual void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    /// <summary>Called when health reaches zero. Override in a subclass to add death effects.</summary>
    protected virtual void Die()
    {
        if (IsDead) return; // guard against double-die
        OnDied?.Invoke();
    }

    private void OnValidate()
    {
        _maxHealth = Mathf.Max(1, _maxHealth);
        _startingHealth = Mathf.Clamp(_startingHealth, 0, _maxHealth);
    }
}