using UnityEngine;

/// <summary>
/// A door sits on the shared edge between exactly two rooms (RegisterRoom is called twice,
/// once per side). It stays closed while either connected room isn't cleared, and opens the
/// moment both are cleared — a room with no enemies counts as cleared immediately.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite _closedSprite;
    [SerializeField] private Sprite _openSprite;

    private Collider2D _collider;
    private RoomController _roomA;
    private RoomController _roomB;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        Close(); // closed by default until registered rooms tell us otherwise
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

    public void Open()
    {
        IsOpen = true;
        if (_collider != null) _collider.enabled = false;
        if (_spriteRenderer != null && _openSprite != null) _spriteRenderer.sprite = _openSprite;
    }

    public void Close()
    {
        IsOpen = false;
        if (_collider != null) _collider.enabled = true;
        if (_spriteRenderer != null && _closedSprite != null) _spriteRenderer.sprite = _closedSprite;
    }

    private void OnDestroy()
    {
        if (_roomA != null) _roomA.OnCleared -= Reevaluate;
        if (_roomB != null) _roomB.OnCleared -= Reevaluate;
    }
}