using UnityEngine;

/// <summary>
/// Plays a hurt sound when this entity takes damage and a death sound when it dies.
/// Works on the player and on enemies — subscribes to HealthComponent.OnDamaged / OnDied,
/// the same pattern HitFlashEffect uses. OnDamaged only fires when a hit actually applies
/// (not dead, not invulnerable, amount > 0), so no sound plays on ignored hits.
///
/// A lethal hit fires BOTH OnDamaged and OnDied. Hurt is skipped when the hit killed this
/// entity (IsDead is already true by then), so only the death sound plays.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class HealthSoundEffect : MonoBehaviour
{
    [Tooltip("Played when this entity takes a non-lethal hit. Leave empty for no sound.")]
    [SerializeField] private SoundSO _hurtSound;

    [Tooltip("Played once when this entity dies. Leave empty for no sound.")]
    [SerializeField] private SoundSO _deathSound;

    private HealthComponent _health;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
    }

    private void OnEnable()
    {
        _health.OnDamaged += HandleDamaged;
        _health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        _health.OnDamaged -= HandleDamaged;
        _health.OnDied -= HandleDied;
    }

    private void HandleDamaged(int amount)
    {
        if (_health.IsDead) return; // lethal hit — the death sound covers it

        SFXManager.Instance?.Play(_hurtSound);
    }

    private void HandleDied()
    {
        SFXManager.Instance?.Play(_deathSound);
    }
}