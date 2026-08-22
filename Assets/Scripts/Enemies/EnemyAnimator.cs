using System;
using UnityEngine;

/// <summary>
/// Drives an enemy's Animator (IsMoving / Attack / Die) and SpriteRenderer.flipX. Turns the
/// enemy to face the player (simple left/right flip) whenever the player is inside this enemy's
/// own room (EnemyRoomGuard.IsPlayerInRoom) — i.e. while chasing or otherwise "aware" of the
/// player. Outside of that it just keeps its last facing instead of flipping randomly.
///
/// Other enemy scripts (EnemyMeleeContactDamage, EnemyRangedAttack, EnemyHealth) call the public
/// PlayAttack()/PlayDeath() methods here instead of touching the Animator directly — same
/// one-way coupling PlayerAnimator uses toward PlayerMovement.
///
/// DEATH TIMING: OnDeathAnimationComplete() is meant to be hooked up as an Animation Event on the
/// last frame of the Death clip. If no Animator is assigned (no death art yet) OR that event is
/// never wired up, a fallback timer (_deathFallbackDuration) fires OnDeathAnimationFinished
/// anyway, so EnemyHealth is never left waiting forever for a GameObject that should already be
/// destroyed.
///
/// ATTACK RELEASE TIMING: same pattern as death. OnAttackAnimationComplete() is meant to be
/// hooked up as an Animation Event on the Attack clip's "release" frame (the frame the shot
/// should actually leave). If no Animator is assigned, or that event is never wired up, a
/// fallback timer (_attackReleaseFallbackDuration) fires OnAttackReleased anyway, so
/// EnemyRangedAttack is never left waiting forever for a projectile that should've launched.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyAnimator : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("Facing")]
    [Tooltip("If true, the sprite's default artwork faces RIGHT (flipX=true mirrors it to face left). Uncheck if your sprite sheet was drawn facing left by default.")]
    [SerializeField] private bool _defaultFacesRight = true;

    [Header("Death")]
    [Tooltip("Safety net only — used if no Animator is assigned, or the Death clip's Animation Event never calls OnDeathAnimationComplete().")]
    [SerializeField] private float _deathFallbackDuration = 1f;

    [Header("Attack")]
    [Tooltip("Safety net only — used if no Animator is assigned, or the Attack clip's Animation Event never calls OnAttackAnimationComplete(). Should be set a little longer than the Attack clip's actual release frame so the real event normally wins.")]
    [SerializeField] private float _attackReleaseFallbackDuration = 0.5f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private EnemyRoomGuard _roomGuard;
    private Rigidbody2D _rb;
    private bool _facingRight;
    private bool _deathHandled;

    /// <summary>Fired once when the death animation is considered finished — either via the
    /// Animation Event (OnDeathAnimationComplete) or the fallback timer. EnemyHealth subscribes
    /// to know when it's safe to Destroy() the GameObject.</summary>
    public event Action OnDeathAnimationFinished;

    /// <summary>Fired once per attack when the Attack animation's release point is reached —
    /// either via the Animation Event (OnAttackAnimationComplete) or the fallback timer.
    /// EnemyRangedAttack subscribes to know exactly when to spawn the projectile.</summary>
    public event Action OnAttackReleased;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();

        _roomGuard = GetComponent<EnemyRoomGuard>(); // optional — enemies with no guard just never auto-turn
        _rb = GetComponent<Rigidbody2D>();

        _facingRight = _defaultFacesRight; // matches the sprite's un-flipped default orientation
    }

    private void Update()
    {
        if (_animator != null && _rb != null)
            _animator.SetBool(IsMovingHash, _rb.linearVelocity.sqrMagnitude > 0.01f);

        UpdateFacing();
    }

    private void UpdateFacing()
    {
        if (_roomGuard == null || !_roomGuard.IsPlayerInRoom || PlayerMovement.Instance == null)
            return; // not tracking the player right now — keep last facing

        float dx = PlayerMovement.Instance.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return; // avoid flicker when standing almost directly on top of the player

        bool wantsFacingRight = dx > 0f;
        if (wantsFacingRight == _facingRight) return;

        _facingRight = wantsFacingRight;
        _spriteRenderer.flipX = _defaultFacesRight ? !_facingRight : _facingRight;
    }

    /// <summary>Call when starting a ranged attack windup. Fires the Attack trigger and arms
    /// either the real release event (via the Animation Event) or the fallback timer, whichever
    /// comes first. If no Animator is assigned at all, fires OnAttackReleased immediately —
    /// same "no art yet" fallback PlayDeath() already uses for death.</summary>
    public void PlayAttack()
    {
        if (_animator == null)
        {
            FinishAttackRelease();
            return;
        }

        _animator.SetTrigger(AttackHash);

        CancelInvoke(nameof(FinishAttackRelease));
        Invoke(nameof(FinishAttackRelease), Mathf.Max(0.01f, _attackReleaseFallbackDuration));
    }

    /// <summary>Hook this up as an Animation Event on the Attack clip's release frame (the frame
    /// the projectile should actually spawn).</summary>
    public void OnAttackAnimationComplete()
    {
        CancelInvoke(nameof(FinishAttackRelease));
        FinishAttackRelease();
    }

    private void FinishAttackRelease()
    {
        // Invoke() may still be pending if OnAttackAnimationComplete already fired this frame —
        // CancelInvoke there already handles that, this just guards a direct double-call.
        CancelInvoke(nameof(FinishAttackRelease));
        OnAttackReleased?.Invoke();
    }

    /// <summary>Call once, from EnemyHealth, when this enemy dies.</summary>
    public void PlayDeath()
    {
        if (_deathHandled) return;
        _deathHandled = true;

        if (_animator == null)
        {
            FinishDeath(); // no death art yet — finish immediately, same as the old instant-destroy behavior
            return;
        }

        _animator.SetTrigger(DieHash);
        Invoke(nameof(FinishDeath), _deathFallbackDuration);
    }

    /// <summary>Hook this up as an Animation Event on the last frame of the Death clip.</summary>
    public void OnDeathAnimationComplete()
    {
        CancelInvoke(nameof(FinishDeath));
        FinishDeath();
    }

    private void FinishDeath()
    {
        // Invoke() may still be pending if OnDeathAnimationComplete already fired this frame —
        // CancelInvoke there already handles that, this just guards a direct double-call.
        CancelInvoke(nameof(FinishDeath));
        OnDeathAnimationFinished?.Invoke();
    }
}