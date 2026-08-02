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

    [Header("Colors")]
    [SerializeField] private Color _currentRoomColor = Color.white;
    [SerializeField] private Color _visitedRoomColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color _knownUnvisitedColor = new Color(0.28f, 0.28f, 0.28f, 1f);

    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    private readonly Dictionary<Vector2Int, Room> _roomsByLogicalCoord = new();
    private readonly Dictionary<Vector2Int, Image> _icons = new(); // Back to just simple Images!
    private readonly HashSet<Vector2Int> _visited = new();
    private readonly HashSet<Vector2Int> _known = new();

    private Vector2Int? _currentLogicalCoord;
    private int _strideX;
    private int _strideY;

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
    }

    private void OnDestroy()
    {
        if (RoomCamera.Instance != null)
            RoomCamera.Instance.OnRoomEntered -= HandleRoomEntered;
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

        // This keeps the fog-of-war working properly so you only see rooms connected by paths
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

    private void RepositionIcons()
    {
        if (!_currentLogicalCoord.HasValue) return;
        Vector2Int origin = _currentLogicalCoord.Value;

        foreach (var kv in _icons)
        {
            Image image = kv.Value;
            Vector2Int relative = kv.Key - origin;
            image.rectTransform.anchoredPosition = new Vector2(
                relative.x * (_cellSize + _cellSpacing),
                relative.y * (_cellSize + _cellSpacing));
        }
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