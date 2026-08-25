using UnityEngine;

/// <summary>
/// A door sits on a room's own exterior edge. It stays OPEN by default — including while a
/// connected room has active enemies, as long as the player hasn't actually entered that room
/// yet — and locks CLOSED only once the player is standing inside a room that has active enemies
/// (RoomController.ShouldLockDoors), reopening the moment that room reports cleared. This is
/// what lets the player freely walk INTO a room full of enemies, then traps them there Isaac-
/// style until it's cleared, instead of the doorway itself being blocked before they ever arrive.
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
///
/// SAFETY NET: Open() also arms a fallback timer (_colliderFallbackDuration) that forces the
/// collider disabled even if OnOpenAnimationComplete's Animation Event was never wired up on a
/// given door's Opening clip. Without this, a door with a missing/misconfigured Animation Event
/// would visually/logically be "open" (IsOpen == true) but the player could never actually walk
/// through it — same class of silent-failure bug the Attack/Death fallback timers on
/// EnemyAnimator already guard against. Close() doesn't need an equivalent fallback: it already
/// re-enables the collider unconditionally and immediately, regardless of animation.
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

    [Header("Safety Net")]
    [Tooltip("Used only if this door has an Animator assigned but its Opening clip never calls OnOpenAnimationComplete() via an Animation Event (easy to forget wiring up per-door-prefab/variant). Forces the collider disabled after this many seconds so the door can never get silently stuck 'open but still blocking'. Set a little longer than the actual opening animation so the real Animation Event normally wins.")]
    [SerializeField] private float _colliderFallbackDuration = 0.5f;

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

        room.OnLockStateChanged += Reevaluate;
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

    /// <summary>A door locks the instant EITHER connected room wants to lock (the player is
    /// standing inside it with active enemies) — a room with no controller registered (e.g. an
    /// unresolved neighbor) never blocks, same as the old null-check behavior.</summary>
    private void Reevaluate()
    {
        bool aBlocks = _roomA != null && _roomA.ShouldLockDoors;
        bool bBlocks = _roomB != null && _roomB.ShouldLockDoors;

        if (!aBlocks && !bBlocks) Open();
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
            // Collider is normally disabled by OnOpenAnimationComplete() (Animation Event) —
            // but arm a fallback in case that event isn't wired up on this door's clip, so the
            // collider can never get stuck enabled forever. See class doc.
            CancelInvoke(nameof(ForceColliderDisabledFallback));
            Invoke(nameof(ForceColliderDisabledFallback), Mathf.Max(0.01f, _colliderFallbackDuration));
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

        // Cancel any pending open-collider fallback from a previous Open() — otherwise a stale
        // Invoke could later re-disable the collider after Close() has already correctly
        // re-enabled it (e.g. the door gets re-locked shortly after opening).
        CancelInvoke(nameof(ForceColliderDisabledFallback));

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
        CancelInvoke(nameof(ForceColliderDisabledFallback));
        if (_collider != null) _collider.enabled = false;
    }

    /// <summary>Optional: hook this up as an Animation Event on the last frame of the Closing clip
    /// if you want the collider to stay disabled until the closing animation visually finishes.
    /// By default Close() already re-enables it immediately for safety.</summary>
    public void OnCloseAnimationComplete()
    {
        if (_collider != null) _collider.enabled = true;
    }

    /// <summary>Fallback for OnOpenAnimationComplete — see Open()/class doc.</summary>
    private void ForceColliderDisabledFallback()
    {
        if (_collider != null) _collider.enabled = false;
    }

    private void OnDestroy()
    {
        if (_roomA != null) _roomA.OnLockStateChanged -= Reevaluate;
        if (_roomB != null) _roomB.OnLockStateChanged -= Reevaluate;
    }
}