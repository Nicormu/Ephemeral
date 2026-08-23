using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _mapContainer;
    [SerializeField] private GameObject _roomIconPrefab;

    [Header("Layout (Set Spacing to 0 for seamless grid)")]
    [SerializeField] private float _cellSize = 30f;
    [SerializeField] private float _cellSpacing = 0f;
    [SerializeField] private int _visibleRadiusX = 2;
    [SerializeField] private int _visibleRadiusY = 2;

    [Header("Animation")]
    [Tooltip("Seconds for minimap icons to glide into their new position when the current room changes.")]
    [SerializeField] private float _repositionDuration = 0.25f;

    [Header("Colors")]
    [SerializeField] private Color _currentRoomColor = Color.white;
    [SerializeField] private Color _visitedRoomColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color _knownUnvisitedColor = new Color(0.28f, 0.28f, 0.28f, 1f);

    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    private readonly Dictionary<Vector2Int, Room> _roomsByLogicalCoord = new();
    private readonly Dictionary<Vector2Int, Image> _icons = new();
    private readonly HashSet<Vector2Int> _visited = new();
    private readonly HashSet<Vector2Int> _known = new();

    private Vector2Int? _currentLogicalCoord;
    private int _strideX;
    private int _strideY;
    private Coroutine _repositionRoutine;

    private void Start()
    {
        _strideX = RoomTemplate.RoomTileSize.x + DungeonGridConstants.RoomGap;
        _strideY = RoomTemplate.RoomTileSize.y + DungeonGridConstants.RoomGap;

        ResizeMapContainer();
        BuildRoomLookup();
        SpawnIcons();

        if (RoomCamera.Instance != null)
        {
            RoomCamera.Instance.OnRoomEntered += HandleRoomEntered;

            if (RoomCamera.Instance.CurrentRoom.HasValue)
                HandleRoomEntered(RoomCamera.Instance.CurrentRoom.Value);
        }

        // Rebuild everything from scratch whenever the dungeon regenerates in place (e.g. R-key
        // reset, or the post-death reset via DungeonResetController) — Regenerate() doesn't
        // reload the scene, so without this the minimap keeps showing the previous dungeon's
        // room lookup, icons, and visited/known state forever after the first generation.
        if (DungeonManager.Instance != null)
            DungeonManager.Instance.OnDungeonGenerated += HandleDungeonGenerated;
    }

    private void OnDestroy()
    {
        if (RoomCamera.Instance != null)
            RoomCamera.Instance.OnRoomEntered -= HandleRoomEntered;

        if (DungeonManager.Instance != null)
            DungeonManager.Instance.OnDungeonGenerated -= HandleDungeonGenerated;

        if (_repositionRoutine != null)
            StopCoroutine(_repositionRoutine);
    }

    /// <summary>Full reset for a fresh dungeon: destroys the previous generation's icons, clears
    /// all visited/known/current-room state, then rebuilds the room lookup and icon set from the
    /// new DungeonManager.Instance.Rooms. Resyncs the "current room" from DungeonManager's own
    /// Start room position directly — NOT from RoomCamera.Instance.CurrentRoom — because at the
    /// moment this event fires, RoomCamera hasn't run its own LateUpdate yet this frame and would
    /// still be reporting the OLD dungeon's room for a moment. The player is guaranteed to be
    /// standing in the new Start room by this point (DungeonManager.Initialize() positions them
    /// before firing OnDungeonGenerated), so that's the reliable source of truth here.</summary>
    private void HandleDungeonGenerated()
    {
        if (_repositionRoutine != null)
        {
            StopCoroutine(_repositionRoutine);
            _repositionRoutine = null;
        }

        foreach (var icon in _icons.Values)
            if (icon != null) Destroy(icon.gameObject);

        _icons.Clear();
        _visited.Clear();
        _known.Clear();
        _currentLogicalCoord = null;

        BuildRoomLookup();
        SpawnIcons();

        if (DungeonManager.Instance != null)
        {
            Room? startRoom = DungeonManager.Instance.GetRoomAtGrid(DungeonManager.Instance.StartGridPosition);
            if (startRoom.HasValue)
                HandleRoomEntered(startRoom.Value);
        }
    }

    private void ResizeMapContainer()
    {
        if (_mapContainer == null) return;
        float step = _cellSize + _cellSpacing;
        float width = (2 * _visibleRadiusX + 1) * step;
        float height = (2 * _visibleRadiusY + 1) * step;
        _mapContainer.sizeDelta = new Vector2(width, height);
    }

    private void BuildRoomLookup()
    {
        _roomsByLogicalCoord.Clear();
        if (DungeonManager.Instance == null || DungeonManager.Instance.Rooms == null) return;

        foreach (var room in DungeonManager.Instance.Rooms)
            _roomsByLogicalCoord[ToLogicalCoord(room.GridPos)] = room;
    }

    private void SpawnIcons()
    {
        if (_mapContainer == null || _roomIconPrefab == null) return;

        foreach (var kv in _roomsByLogicalCoord)
        {
            GameObject instance = Instantiate(_roomIconPrefab, _mapContainer);
            instance.name = $"RoomIcon_{kv.Key.x}_{kv.Key.y}";

            var rt = instance.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(_cellSize, _cellSize);

            var image = instance.GetComponent<Image>();

            instance.SetActive(false);
            _icons[kv.Key] = image;
        }
    }

    private void HandleRoomEntered(Room room)
    {
        Vector2Int logical = ToLogicalCoord(room.GridPos);

        _currentLogicalCoord = logical;
        _visited.Add(logical);
        _known.Remove(logical);

        // Only reveal rooms connected by an actual door path.
        foreach (var dir in AllDirections)
        {
            if ((room.Doors & dir) == 0) continue;

            Vector2Int neighborLogical = logical + DungeonGeometry.UnitOffset(dir);
            if (_visited.Contains(neighborLogical)) continue;
            if (_roomsByLogicalCoord.ContainsKey(neighborLogical))
                _known.Add(neighborLogical);
        }

        RefreshVisuals();
        RepositionIcons();
    }

    /// <summary>
    /// Smooth glide of every icon toward its new anchored
    /// position relative to the current room, instead of snapping instantly.
    /// </summary>
    private void RepositionIcons()
    {
        if (!_currentLogicalCoord.HasValue) return;

        if (_repositionRoutine != null)
            StopCoroutine(_repositionRoutine);

        _repositionRoutine = StartCoroutine(AnimateReposition(_currentLogicalCoord.Value));
    }

    private IEnumerator AnimateReposition(Vector2Int origin)
    {
        // Snapshot start/target positions once — icons that are currently invisible still get
        // a valid target so they slide in cleanly the instant RefreshVisuals() turns them on.
        var starts = new Dictionary<Vector2Int, Vector2>(_icons.Count);
        var targets = new Dictionary<Vector2Int, Vector2>(_icons.Count);

        foreach (var kv in _icons)
        {
            Vector2Int relative = kv.Key - origin;
            starts[kv.Key] = kv.Value.rectTransform.anchoredPosition;
            targets[kv.Key] = new Vector2(
                relative.x * (_cellSize + _cellSpacing),
                relative.y * (_cellSize + _cellSpacing));
        }

        float duration = Mathf.Max(0.01f, _repositionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            foreach (var kv in _icons)
                kv.Value.rectTransform.anchoredPosition = Vector2.Lerp(starts[kv.Key], targets[kv.Key], t);

            yield return null;
        }

        foreach (var kv in _icons)
            kv.Value.rectTransform.anchoredPosition = targets[kv.Key];

        _repositionRoutine = null;
    }

    private void RefreshVisuals()
    {
        foreach (var kv in _icons)
        {
            Vector2Int coord = kv.Key;
            Image image = kv.Value;

            bool isCurrent = _currentLogicalCoord.HasValue && coord == _currentLogicalCoord.Value;
            bool isVisited = _visited.Contains(coord);
            bool isKnown = _known.Contains(coord);

            if (!isCurrent && !isVisited && !isKnown)
            {
                image.gameObject.SetActive(false);
                continue;
            }

            image.gameObject.SetActive(true);
            image.color = isCurrent ? _currentRoomColor : (isVisited ? _visitedRoomColor : _knownUnvisitedColor);
        }
    }

    private Vector2Int ToLogicalCoord(Vector2Int gridPos) =>
        new Vector2Int(gridPos.x / _strideX, gridPos.y / _strideY);
}