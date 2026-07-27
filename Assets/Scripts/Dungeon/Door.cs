using UnityEngine;

/// <summary>
/// A door sits on the shared edge between exactly two rooms (RegisterRoom is called twice,
/// once per side). It stays closed while either connected room isn't cleared, and opens the
/// moment both are cleared — a room with no enemies counts as cleared immediately.
///
/// Animation: if an Animator is assigned, Open()/Close() fire OpenTrigger/CloseTrigger and the
/// collider is toggled via Animation Events (OnOpenAnimationComplete / OnCloseAnimationComplete)
/// so collision matches what's on screen. If no Animator is assigned, falls back to the old
/// instant sprite-swap behavior.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    [Header("Visuals - Sprite fallback (used only if no Animator is assigned)")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite _closedSprite;
    [SerializeField] private Sprite _openSprite;

    [Header("Visuals - Animation (optional)")]
    [Tooltip("If assigned, Open()/Close() trigger animations instead of swapping sprites instantly.")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _openTriggerName = "OpenTrigger";
    [SerializeField] private string _closeTriggerName = "CloseTrigger";

    private Collider2D _collider;
    private RoomController _roomA;
    private RoomController _roomB;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_animator == null) _animator = GetComponent<Animator>();
        Close(instant: true); // closed by default until registered rooms tell us otherwise
    }

    /// <summary>Called once per adjacent room right after this door is instantiated.</summary>
    public void RegisterRoom(RoomController room)
    {
        if (room == null) return;

        if (_roomA == null) _roomA = room;
        else if (_roomB == null) _roomB = room;

        room.OnCleared += Reevaluate;
        Reevaluate();
    }

    private void Reevaluate()
    {
        bool aCleared = _roomA == null || _roomA.IsCleared;
        bool bCleared = _roomB == null || _roomB.IsCleared;

        if (aCleared && bCleared) Open();
        else Close();
    }

    public void Open(bool instant = false)
    {
        if (IsOpen) return;
        IsOpen = true;

        if (_animator != null && !instant)
        {
            _animator.SetTrigger(_openTriggerName);
            // Collider is disabled by OnOpenAnimationComplete() (Animation Event), not here.
        }
        else
        {
            if (_collider != null) _collider.enabled = false;
            if (_spriteRenderer != null && _openSprite != null) _spriteRenderer.sprite = _openSprite;
        }
    }

    public void Close(bool instant = false)
    {
        if (!IsOpen && !instant) return;
        IsOpen = false;

        // Closing should always re-block immediately, even with animation,
        // so the player can't sneak through mid-animation.
        if (_collider != null) _collider.enabled = true;

        if (_animator != null && !instant)
        {
            _animator.SetTrigger(_closeTriggerName);
        }
        else if (_spriteRenderer != null && _closedSprite != null)
        {
            _spriteRenderer.sprite = _closedSprite;
        }
    }

    /// <summary>Hook this up as an Animation Event on the last frame of the Opening clip.</summary>
    public void OnOpenAnimationComplete()
    {
        if (_collider != null) _collider.enabled = false;
    }

    /// <summary>Optional: hook this up as an Animation Event on the last frame of the Closing clip
    /// if you want the collider to stay disabled until the closing animation visually finishes.
    /// By default Close() already re-enables it immediately for safety.</summary>
    public void OnCloseAnimationComplete()
    {
        if (_collider != null) _collider.enabled = true;
    }

    private void OnDestroy()
    {
        if (_roomA != null) _roomA.OnCleared -= Reevaluate;
        if (_roomB != null) _roomB.OnCleared -= Reevaluate;
    }
}