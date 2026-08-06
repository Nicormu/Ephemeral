using System;
using UnityEngine;

/// <summary>
/// HP and death for enemies. RoomController expects Destroy() (not Deactivate()) on death.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int _maxHealth = 3;

    [Tooltip("Brief invulnerability after taking damage, so multiple hits in one frame/overlap don't stack.")]
    [SerializeField] private float _invulnerabilityDuration = 0.1f;

    private int _currentHealth;
    private float _lastDamageTime = -999f;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool IsInvulnerable => Time.time - _lastDamageTime < _invulnerabilityDuration;
    public bool IsDead { get; private set; }

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDied;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || IsInvulnerable || amount <= 0) return;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        _lastDamageTime = Time.time;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        OnDied?.Invoke();
        Destroy(gameObject); // RoomController's clear-check depends on this.
    }
}