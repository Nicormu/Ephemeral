using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerVoidFall : MonoBehaviour
{
    [Header("Fall Settings")]
    [SerializeField] private int _fallDamage = 1;
    [SerializeField] private float _fallRecoveryDelay = 0.6f;

    private Rigidbody2D _rb;
    private PlayerMovement _movement;
    private Vector3 _lastSafePosition;
    private bool _isFalling;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _movement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        _lastSafePosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (_isFalling || DungeonManager.Instance == null) return;

        Vector2Int cell = DungeonManager.WorldToGridCell(transform.position);
        CellState state = DungeonManager.Instance.GetCellState(cell);

        if (state == CellState.Floor)
            _lastSafePosition = transform.position;
        else if (state == CellState.Void)
            StartCoroutine(FallRoutine());
        // Obstacle cells are handled by their own collider — the player physically
        // can't stand on one, so no case is needed here.
    }

    private IEnumerator FallRoutine()
    {
        _isFalling = true;

        if (_movement != null) _movement.enabled = false;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.TakeDamage(_fallDamage);

        yield return new WaitForSeconds(_fallRecoveryDelay);

        transform.position = _lastSafePosition;

        if (_movement != null) _movement.enabled = true;
        _isFalling = false;
    }
}