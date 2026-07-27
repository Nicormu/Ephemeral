using UnityEngine;

/// <summary>
/// Lives on a child GameObject of a Door, positioned half a tile past the door's own tile
/// (toward the gap between rooms). While the parent Door is open, entering this trigger
/// teleports the player to the door's TeleportDestination (a safe point just inside the
/// connected room) — this is what makes the room-separation architecture feel like walking
/// through a doorway instead of falling into the gap between rooms.
///
/// Requires its own Collider2D with Is Trigger enabled (this script forces that on Awake, but
/// set it in the Inspector too so it's correct in Scene view before Play).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DoorEntryTrigger : MonoBehaviour
{
    [Tooltip("The Door this trigger belongs to. Leave empty to auto-find it on a parent object.")]
    [SerializeField] private Door _parentDoor;

    private void Awake()
    {
        if (_parentDoor == null)
            _parentDoor = GetComponentInParent<Door>();

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (_parentDoor == null)
            Debug.LogWarning($"[DoorEntryTrigger] '{name}' has no Door in its parent hierarchy — it will never teleport anyone.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_parentDoor == null || !_parentDoor.IsOpen) return;

        var movement = other.GetComponent<PlayerMovement>();
        if (movement == null) return;

        movement.TeleportTo(_parentDoor.TeleportDestination);
    }
}