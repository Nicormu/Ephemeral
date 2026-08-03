using UnityEngine;

/// <summary>
/// Drives the player's Animator (IsMoving/Facing) and SpriteRenderer.flipX purely from
/// PlayerMovement's public state — no coupling the other direction. Facing only updates
/// while actually moving; Idle has no directional art, so a stale Facing value while idle
/// is harmless.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    private enum Facing { Down = 0, Up = 1, Side = 2 }

    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int FacingHash = Animator.StringToHash("Facing");

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (PlayerMovement.Instance == null) return;

        bool isMoving = PlayerMovement.Instance.CurrentState == PlayerState.Moving;
        _animator.SetBool(IsMovingHash, isMoving);

        if (!isMoving) return; // keep last Facing value; Idle clip ignores it anyway

        Vector2 dir = PlayerMovement.Instance.Direction;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            _animator.SetInteger(FacingHash, (int)Facing.Side);
            _spriteRenderer.flipX = dir.x < 0f;
        }
        else
        {
            _animator.SetInteger(FacingHash, (int)(dir.y > 0f ? Facing.Up : Facing.Down));
        }
    }
}