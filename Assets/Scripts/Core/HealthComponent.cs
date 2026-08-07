using System;
using UnityEngine;

/// <summary>
/// Shared health logic for any entity that can take damage and die.
/// Use as a component on any GameObject — other scripts interact through it
/// via TakeDamage(), Heal(), or the OnDied / OnHealthChanged events.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int _maxHealth = 3;

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
        _currentHealth = _maxHealth;
    }

    /// <summary>Reconfigure health values at runtime (e.g. from a wrapper class with its own inspector defaults).</summary>
    public void Configure(int maxHealth, float invulnerabilityDuration)
    {
        _maxHealth = Mathf.Max(1, maxHealth);
        // Clamp current health to new max in case it's being called before Awake()
        // (when _currentHealth is still 0), or if max was reduced.
        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
        _invulnerabilityDuration = invulnerabilityDuration;
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

    /// <summary>Heal back to max health. Override in a subclass to disable healing for specific entity types.</summary>
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
}
