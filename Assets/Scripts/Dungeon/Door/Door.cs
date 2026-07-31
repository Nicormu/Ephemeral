using UnityEngine;

/// <summary>
/// A door sits on a room's own exterior edge. It stays closed while either connected room isn't
/// cleared, and opens the moment both are cleared — a room with no enemies counts as cleared
/// immediately.
///
/// Since rooms are separated by a gap (see DungeonGridConstants.RoomGap), each connection
/// between two rooms is now made of TWO independent Door instances — one per room, each on its
/// own edge — instead of one shared tile. Walking through an open door doesn't physically cross
/// the gap: a child EntryTrigger (see DoorEntryTrigger.cs), positioned half a tile past this
/// door's own tile, teleports the player straight to TeleportDestination, which DungeonManager
/// sets to a safe point just inside the connected room.
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

    [Header("Teleport")]
    [Tooltip("Child object (with an isTrigger Collider2D + DoorEntryTrigger) positioned half a tile past this door's own tile, toward the gap. Detects the player crossing the threshold while the door is open. Leave empty if this door shouldn't teleport (e.g. a decorative door).")]
    [SerializeField] private Transform _entryTrigger;

    private Collider2D _collider;
    private RoomController _roomA;
    private RoomController _roomB;

    public bool IsOpen { get; private set; }

    /// <summary>World position the player is sent to when crossing this door's EntryTrigger while open. Set by DungeonManager.PlaceDoor() right after this door is instantiated.</summary>
    public Vector3 TeleportDestination { get; private set; }

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

    /// <summary>Sets where the player lands after crossing this door's threshold while it's open.</summary>
    public void SetTeleportDestination(Vector3 destination)
    {
        TeleportDestination = destination;
    }

    /// <summary>Disables this door's EntryTrigger — used when DungeonManager couldn't resolve a
    /// connected neighbor room, so an accidental crossing never teleports the player somewhere invalid.</summary>
    public void DisableEntryTrigger()
    {
        if (_entryTrigger != null)
            _entryTrigger.gameObject.SetActive(false);
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
            _animator.enabled = true; // Ensure Animator is running
            _animator.SetTrigger(_openTriggerName);
            // Collider is disabled by OnOpenAnimationComplete() (Animation Event), not here.
        }
        else
        {
            if (_collider != null) _collider.enabled = false;
            
            // Disable Animator so it doesn't overwrite our manual sprite change
            if (_animator != null) _animator.enabled = false; 
            
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
            _animator.enabled = true; // Ensure Animator is running
            _animator.SetTrigger(_closeTriggerName);
        }
        else 
        {
            // Disable Animator so it doesn't overwrite our manual sprite change
            if (_animator != null) _animator.enabled = false; 
            
            if (_spriteRenderer != null && _closedSprite != null)
            {
                _spriteRenderer.sprite = _closedSprite;
            }
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