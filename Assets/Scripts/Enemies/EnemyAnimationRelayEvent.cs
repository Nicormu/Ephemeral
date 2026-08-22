using UnityEngine;

/// <summary>
/// Lives on the same GameObject as the Animator when the animated visual is a CHILD of the
/// enemy's root (e.g. root holds physics/collision, a separate child holds the SpriteRenderer +
/// Animator). Unity's Animation Event dropdown can only target methods on components attached to
/// the SAME GameObject as the Animator — it can't reach up into a parent's script. This relay
/// exists purely to satisfy that requirement: the Animation Event calls these methods (which
/// live right next to the Animator), and they immediately forward the call up to EnemyAnimator
/// on the root via GetComponentInParent.
///
/// Add this component to the visual child GameObject (the one with the Animator), then in the
/// Animation window point your Animation Event's Function at THIS script's methods instead of
/// EnemyAnimator's.
/// </summary>
public class EnemyAnimationEventRelay : MonoBehaviour
{
    private EnemyAnimator _enemyAnimator;

    private void Awake()
    {
        _enemyAnimator = GetComponentInParent<EnemyAnimator>();

        if (_enemyAnimator == null)
            Debug.LogWarning($"[EnemyAnimationEventRelay] '{name}' couldn't find an EnemyAnimator in its parent hierarchy — animation events won't do anything.");
    }

    /// <summary>Hook this up as an Animation Event on the Attack clip's release frame.</summary>
    public void OnAttackAnimationComplete() => _enemyAnimator?.OnAttackAnimationComplete();

    /// <summary>Hook this up as an Animation Event on the last frame of the Death clip.</summary>
    public void OnDeathAnimationComplete() => _enemyAnimator?.OnDeathAnimationComplete();
}