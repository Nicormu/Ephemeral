using System.Collections;
using UnityEngine;

/// <summary>
/// Frames the camera to exactly match the bounds of the room the player is currently in —
/// the same rectangle DungeonManager.OnDrawGizmos draws — and pans/zooms to the new room's
/// bounds whenever the player crosses into a different room. Requires an Orthographic camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RoomCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Player transform to track. Leave empty to auto-find PlayerMovement.Instance.")]
    [SerializeField] private Transform _target;

    [Header("Transition")]
    [Tooltip("Seconds to pan/zoom between rooms. Set to 0 for an instant snap.")]
    [SerializeField] private float _transitionDuration = 0.35f;

    [Tooltip("Extra world-space padding added around the room bounds so walls aren't flush against the screen edge.")]
    [SerializeField] private float _padding = 0f;

    private Camera _camera;
    private Vector2Int? _currentRoomGridPos;
    private Coroutine _transitionRoutine;

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        if (!_camera.orthographic)
            Debug.LogWarning("[RoomCamera] Camera is not Orthographic — room framing assumes an "
                + "orthographic camera and won't size correctly in Perspective mode.");
    }

    private void Start()
    {
        if (_target == null && PlayerMovement.Instance != null)
            _target = PlayerMovement.Instance.transform;

        SnapToCurrentRoom();
    }

    private void LateUpdate()
    {
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
            MoveToRoom(room.Value, instant: _transitionDuration <= 0f);
        }
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