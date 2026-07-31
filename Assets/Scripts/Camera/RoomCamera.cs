using System.Collections;
using UnityEngine;

/// <summary>
/// Frames the camera to exactly match the bounds of the room the player is currently in —
/// the same rectangle DungeonManager.OnDrawGizmos draws. Requires an Orthographic camera.
///
/// Two ways a room change can happen:
///
///   1. DOOR CROSSING (the normal case): DoorEntryTrigger calls BeginRoomTransition() directly.
///      This locks the player's movement and lerps BOTH the player's position (from the door
///      threshold to the entry point in the next room) AND the camera's position/zoom (from the
///      old room's framing to the new room's framing) over the same duration. Because both move
///      together, the old room visually slides/pushes off screen while the new one is revealed —
///      the classic Isaac-style transition — instead of an instant cut + instant teleport.
///
///   2. ANY OTHER TELEPORT (hazard recovery, initial spawn, etc.): the player's position changes
///      some other way and LateUpdate() here just notices the room changed and reframes on its
///      own, controlled by _instantSnap / _transitionDuration below. This path is intentionally
///      simpler since those teleports aren't a "crossing" the player should see happen smoothly.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RoomCamera : MonoBehaviour
{
    public static RoomCamera Instance { get; private set; }

    [Header("Target")]
    [Tooltip("Player transform to track. Leave empty to auto-find PlayerMovement.Instance.")]
    [SerializeField] private Transform _target;

    [Header("Door Transition (Isaac-style push)")]
    [Tooltip("Seconds for the camera-pan + player-glide when crossing through a door.")]
    [SerializeField] private float _doorTransitionDuration = 0.4f;

    [Header("Fallback Transition Style (non-door teleports)")]
    [Tooltip("Isaac-style hard cut: the camera snaps instantly the moment the player's cell belongs to a new room. Turn off for a smooth pan/zoom instead. Only applies when the room change wasn't triggered via BeginRoomTransition (e.g. hazard recovery, spawn).")]
    [SerializeField] private bool _instantSnap = true;

    [Tooltip("Seconds to pan/zoom for a non-door room change. Only used when Instant Snap is OFF.")]
    [SerializeField] private float _transitionDuration = 0.35f;

    [Tooltip("Extra world-space padding added around the room bounds so walls aren't flush against the screen edge.")]
    [SerializeField] private float _padding = 0f;

    private Camera _camera;
    private Vector2Int? _currentRoomGridPos;
    private Coroutine _transitionRoutine;
    private bool _isDoorTransitioning;

    private void Awake()
    {
        Instance = this;
        _camera = GetComponent<Camera>();

        if (!_camera.orthographic)
            Debug.LogWarning("[RoomCamera] Camera is not Orthographic — room framing assumes an "
                + "orthographic camera and won't size correctly in Perspective mode.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (_target == null && PlayerMovement.Instance != null)
            _target = PlayerMovement.Instance.transform;

        SnapToCurrentRoom();
    }

    private void LateUpdate()
    {
        // A door crossing is already driving the player's position and the camera's framing
        // frame-by-frame in TransitionAcrossRooms() — don't fight it here.
        if (_isDoorTransitioning) return;

        if (_target == null)
        {
            if (PlayerMovement.Instance != null) _target = PlayerMovement.Instance.transform;
            return;
        }

        if (DungeonManager.Instance == null) return;

        Vector2Int cell = DungeonManager.WorldToGridCell(_target.position);
        Room? room = DungeonManager.Instance.GetRoomAtGrid(cell);
        if (room == null) return; // player is momentarily over a Void cell — keep current framing

        if (_currentRoomGridPos == null || room.Value.GridPos != _currentRoomGridPos.Value)
        {
            _currentRoomGridPos = room.Value.GridPos;
            MoveToRoom(room.Value, instant: _instantSnap || _transitionDuration <= 0f);
        }
    }

    /// <summary>
    /// Call this instead of PlayerMovement.TeleportTo() when the player is crossing a door.
    /// Locks player control, then glides the player to 'destination' and pans/zooms the camera
    /// to whatever room contains that destination, in lockstep, over _doorTransitionDuration.
    /// Falls back to an instant teleport if the destination isn't inside any known room (should
    /// only happen if dungeon data is missing) or if a transition is already in progress.
    /// </summary>
    public void BeginRoomTransition(PlayerMovement player, Vector3 destination)
    {
        if (player == null) return;

        if (_isDoorTransitioning)
        {
            // Already mid-transition — ignore a second trigger overlap rather than fighting it.
            return;
        }

        if (DungeonManager.Instance == null)
        {
            player.TeleportTo(destination);
            return;
        }

        Vector2Int destCell = DungeonManager.WorldToGridCell(destination);
        Room? targetRoom = DungeonManager.Instance.GetRoomAtGrid(destCell);

        if (targetRoom == null)
        {
            Debug.LogWarning("[RoomCamera] BeginRoomTransition destination isn't inside any known room — falling back to instant teleport.");
            player.TeleportTo(destination);
            return;
        }

        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionAcrossRooms(player, destination, targetRoom.Value));
    }

    private IEnumerator TransitionAcrossRooms(PlayerMovement player, Vector3 destination, Room targetRoom)
    {
        _isDoorTransitioning = true;
        player.SetControlEnabled(false);

        Vector3 startPlayerPos = player.transform.position;
        Vector3 startCamPos = transform.position;
        float startCamSize = _camera.orthographicSize;

        Vector3 targetCamPos = new Vector3(
            targetRoom.GridPos.x + targetRoom.Width / 2f,
            targetRoom.GridPos.y + targetRoom.Height / 2f,
            transform.position.z);
        float targetCamSize = CalculateOrthoSize(targetRoom.Width, targetRoom.Height);

        float duration = Mathf.Max(0.01f, _doorTransitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            player.transform.position = Vector3.Lerp(startPlayerPos, destination, t);
            transform.position = Vector3.Lerp(startCamPos, targetCamPos, t);
            _camera.orthographicSize = Mathf.Lerp(startCamSize, targetCamSize, t);

            yield return null;
        }

        player.transform.position = destination;
        transform.position = targetCamPos;
        _camera.orthographicSize = targetCamSize;

        _currentRoomGridPos = targetRoom.GridPos;

        player.SetControlEnabled(true);
        _isDoorTransitioning = false;
        _transitionRoutine = null;
    }

    private void SnapToCurrentRoom()
    {
        if (_target == null || DungeonManager.Instance == null) return;

        Vector2Int cell = DungeonManager.WorldToGridCell(_target.position);
        Room? room = DungeonManager.Instance.GetRoomAtGrid(cell);
        if (room == null) return;

        _currentRoomGridPos = room.Value.GridPos;
        MoveToRoom(room.Value, instant: true);
    }

    private void MoveToRoom(Room room, bool instant)
    {
        Vector3 targetPos = new Vector3(
            room.GridPos.x + room.Width / 2f,
            room.GridPos.y + room.Height / 2f,
            transform.position.z);

        float targetSize = CalculateOrthoSize(room.Width, room.Height);

        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);

        if (instant)
        {
            transform.position = targetPos;
            _camera.orthographicSize = targetSize;
        }
        else
        {
            _transitionRoutine = StartCoroutine(TransitionTo(targetPos, targetSize));
        }
    }

    /// <summary>Ortho size needed so the room fits fully on screen at the current aspect ratio.</summary>
    private float CalculateOrthoSize(int roomWidth, int roomHeight)
    {
        float halfHeight = roomHeight / 2f + _padding;
        float halfWidthAsHeight = (roomWidth / 2f + _padding) / _camera.aspect;
        return Mathf.Max(halfHeight, halfWidthAsHeight);
    }

    private IEnumerator TransitionTo(Vector3 targetPos, float targetSize)
    {
        Vector3 startPos = transform.position;
        float startSize = _camera.orthographicSize;
        float elapsed = 0f;

        while (elapsed < _transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / _transitionDuration);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            _camera.orthographicSize = Mathf.Lerp(startSize, targetSize, t);

            yield return null;
        }

        transform.position = targetPos;
        _camera.orthographicSize = targetSize;
        _transitionRoutine = null;
    }
}