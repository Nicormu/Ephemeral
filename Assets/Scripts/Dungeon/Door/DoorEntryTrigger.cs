using UnityEngine;

/// <summary>
/// Lives on a child GameObject of a Door, positioned half a tile past the door's own tile
/// (toward the gap between rooms). While the parent Door is open, entering this trigger starts
/// the room transition — RoomCamera.BeginRoomTransition glides the player to the door's
/// TeleportDestination (a safe point just inside the connected room) while panning/zooming the
/// camera to match, producing an Isaac-style push/reveal instead of an instant cut.
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

        // Isaac-style push transition: camera pans/zooms to the next room while the player
        // glides across the gap, instead of both cutting instantly. Falls back to an instant
        // teleport if there's no RoomCamera in the scene (shouldn't normally happen).
        if (RoomCamera.Instance != null)
            RoomCamera.Instance.BeginRoomTransition(movement, _parentDoor.TeleportDestination);
        else
            movement.TeleportTo(_parentDoor.TeleportDestination);
    }
}