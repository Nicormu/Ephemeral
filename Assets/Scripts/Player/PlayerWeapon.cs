using UnityEngine;

/// <summary>
/// Simple placeholder player weapon: on attack input, deals a fixed amount of instant damage to
/// every enemy within _attackRange of _attackPoint (defaults to the player's own transform — no
/// facing/direction logic yet, since there's no attack animation to sync to). Gated by
/// _attackCooldown so holding/mashing the input doesn't spam damage every frame.
///
/// Audio: _swingSound plays on every attack; _hitSound plays once per attack, only if at least
/// one enemy actually took damage.
///
/// This is deliberately minimal so the game is playable today — swap it out later for a real
/// melee swing hitbox, a projectile, weapon animations, etc. EnemyHealth.TakeDamage is the same
/// entry point any future weapon system should call, so nothing else needs to change when you do.
/// </summary>
public class PlayerWeapon : MonoBehaviour
{
    [Tooltip("Damage dealt to each enemy hit per attack.")]
    [SerializeField] private int _damage = 13;

    [Tooltip("Radius (world units) around the attack point that gets checked for enemies.")]
    [SerializeField] private float _attackRange = 1.2f;

    [Tooltip("Minimum seconds between attacks.")]
    [SerializeField] private float _attackCooldown = 0.3f;

    [Tooltip("This weapon's knockback strength. The enemy's own Weight (on its EnemyKnockback component) divides this to get how far it actually gets pushed — e.g. Weight 1 travels twice as far as Weight 2 from the same hit. Set to 0 for a weapon that deals damage but never knocks back.")]
    [SerializeField] private float _knockbackPower = 6f;

    [Tooltip("Point the attack radius is centered on. Leave empty to use this GameObject's own transform.")]
    [SerializeField] private Transform _attackPoint;

    [Tooltip("Key/button that triggers an attack.")]
    [SerializeField] private KeyCode _attackKey = KeyCode.Mouse0;

    [Header("Audio")]
    [Tooltip("Played every time an attack is performed, whether or not it hits anything.")]
    [SerializeField] private SoundSO _swingSound;

    [Tooltip("Played once per attack, only if at least one enemy actually took damage. Leave empty if the enemy's own hurt sound (HealthSoundEffect) already works as impact feedback.")]
    [SerializeField] private SoundSO _hitSound;

    private float _lastAttackTime = -999f;

    private void Awake()
    {
        if (_attackPoint == null) _attackPoint = transform;
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_attackKey)) return;
        if (Time.time - _lastAttackTime < _attackCooldown) return;

        Attack();
    }

    private void Attack()
    {
        _lastAttackTime = Time.time;

        SFXManager.Instance?.Play(_swingSound);

        bool hitAnyEnemy = false;

        Collider2D[] hits = Physics2D.OverlapCircleAll(_attackPoint.position, _attackRange);
        foreach (var hit in hits)
        {
            var enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth == null) continue;

            // Same conditions HealthComponent.TakeDamage uses to ignore a hit — checking here
            // lets us know whether this hit will really land, so _hitSound only plays on real hits.
            if (enemyHealth.IsDead || enemyHealth.IsInvulnerable) continue;

            enemyHealth.TakeDamage(_damage, _attackPoint.position, _knockbackPower);
            hitAnyEnemy = true;
        }

        if (hitAnyEnemy)
            SFXManager.Instance?.Play(_hitSound);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = _attackPoint != null ? _attackPoint.position : transform.position;
        Gizmos.DrawWireSphere(center, _attackRange);
    }
}