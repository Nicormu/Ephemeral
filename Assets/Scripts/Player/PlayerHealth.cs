using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Health")]
    [SerializeField] private int _maxHealth = 6;

    [Tooltip("Brief invulnerability window after taking damage, so falling repeatedly doesn't chain-damage.")]
    [SerializeField] private float _invulnerabilityDuration = 1f;

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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _currentHealth = _maxHealth;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
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

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    private void Die()
    {
        OnDied?.Invoke();
        Debug.Log("[PlayerHealth] Player died.");
        // Hook your game-over screen / respawn flow to the OnDied event.
    }
}