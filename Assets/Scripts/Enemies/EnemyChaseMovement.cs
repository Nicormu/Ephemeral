using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves this enemy toward the player whenever the player is within _detectionRange AND inside
/// this enemy's own room (see EnemyRoomGuard) — a room wall is now a hard detection boundary
/// regardless of how large _detectionRange is set. Add this component to make an enemy "track" —
/// omit it for a stationary enemy (e.g. a turret using only EnemyRangedAttack).
///
/// TWO MOVEMENT MODES, controlled by _avoidsObstacles:
///   - OFF (default): straight-line chase — moves directly at the player's current position.
///     Correct for FLYING enemies (a FlightComponent already makes them physically pass over
///     rocks and takes them off hazard/Void damage — see FlightComponent/EnemyHazardDetector),
///     and for any enemy that should simply beeline regardless of terrain.
///   - ON: GROUNDED chase. The enemy paths around blocking obstacles (rocks), Void cells (pits),
///     AND walkable hazard obstacles (fire, spikes, etc. — avoided here even though they don't
///     physically block movement, so a grounded enemy doesn't shrug off hazard damage the way it
///     currently could via EnemyHazardDetector). Uses EnemyPathfinder's 4-directional BFS,
///     searched only within this enemy's own room (via EnemyRoomGuard.OwnerRoomData) since
///     chasing is already confined there. If no path currently exists, the enemy holds still —
///     the next attempt happens automatically the moment the player's grid cell changes (see
///     MoveAlongPath). Turn this ON for any grounded/land enemy prefab.
///
/// BUG FIX: chasing never checked whether the enemy was dead, so a bat still slid toward the
/// player for the entire death animation before actually being destroyed. Guarded behind
/// EnemyHealth.IsDead now, same pattern EnemyHazardDetector already uses.
///
/// SUPPRESSION: other systems (EnemyKnockback, EnemyRangedAttack) that need chase movement to
/// briefly stop driving velocity call RequestSuppress()/ReleaseSuppress() instead of toggling
/// `enabled` directly — see those classes' docs for why. Applies identically in both movement
/// modes above.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyRoomGuard))]
public class EnemyChaseMovement : MonoBehaviour, IEnemyResettable
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 1.5f;

    [Tooltip("Player must be within this world-space distance for the enemy to start chasing. Set very high to always chase — still only applies while the player is in this enemy's own room, see EnemyRoomGuard.")]
    [SerializeField] private float _detectionRange = 8f;

    [Tooltip("Stops closing the distance once this close, so melee enemies don't jitter on top of the player.")]
    [SerializeField] private float _stoppingDistance = 0.6f;

    [Header("Grounded Pathfinding")]
    [Tooltip("If true, this is a GROUNDED/land enemy: it paths around blocking obstacles, Void cells, and walkable hazards instead of moving straight at the player. Leave off for flying enemies or any enemy that should just beeline. See class doc for details.")]
    [SerializeField] private bool _avoidsObstacles = false;

    [Tooltip("Distance (world units) from a path waypoint's cell center at which it's considered reached and the enemy advances to the next one. Only relevant while Avoids Obstacles is on.")]
    [SerializeField] private float _waypointReachedDistance = 0.15f;

    private Rigidbody2D _rb;
    private EnemyRoomGuard _roomGuard;
    private EnemyHealth _health; // optional — enemies with no EnemyHealth just never get stopped on death

    private int _suppressCount;

    // -- grounded pathfinding state --
    private List<Vector2Int> _currentPath;
    private int _pathIndex;
    private Vector2Int? _lastPlannedPlayerCell;

    /// <summary>True while one or more systems (knockback, attack windup, ...) have requested
    /// chase movement be paused. FixedUpdate zeroes velocity and returns early while this is true.</summary>
    public bool IsSuppressed => _suppressCount > 0;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _roomGuard = GetComponent<EnemyRoomGuard>();
        _health = GetComponent<EnemyHealth>();
    }

    /// <summary>Call to pause chase movement. Safe to call from multiple systems at once — chase
    /// only resumes once every caller has matched this with ReleaseSuppress().</summary>
    public void RequestSuppress() => _suppressCount++;

    /// <summary>Releases one suppression request. No-ops (and never goes negative) if called more
    /// times than RequestSuppress() — same defensive clamping as everywhere else in this codebase
    /// that tracks a count.</summary>
    public void ReleaseSuppress() => _suppressCount = Mathf.Max(0, _suppressCount - 1);

    private void FixedUpdate()
    {
        if (_health != null && _health.IsDead)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (IsSuppressed)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (PlayerMovement.Instance == null || !_roomGuard.IsPlayerInRoom)
        {
            _rb.linearVelocity = Vector2.zero;
            ClearPath(); // stale path shouldn't survive the player leaving/re-entering the room
            return;
        }

        Vector2 toPlayer = (Vector2)PlayerMovement.Instance.transform.position - _rb.position;
        float distance = toPlayer.magnitude;

        if (distance > _detectionRange || distance <= _stoppingDistance)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_avoidsObstacles)
            MoveAlongPath();
        else
            _rb.linearVelocity = toPlayer.normalized * _moveSpeed;
    }

    /// <summary>Grounded movement mode: follows _currentPath's waypoints toward the player,
    /// recomputing the path only when the player has moved to a different grid cell than the one
    /// this path was last planned against (cheap, and naturally satisfies "retry once a way might
    /// exist" without a separate timer).</summary>
    private void MoveAlongPath()
    {
        Vector2Int enemyCell = DungeonManager.WorldToGridCell(_rb.position);
        Vector2Int playerCell = DungeonManager.WorldToGridCell(PlayerMovement.Instance.transform.position);

        bool needsRecompute = _lastPlannedPlayerCell == null || _lastPlannedPlayerCell.Value != playerCell;
        if (needsRecompute)
        {
            RecomputePath(enemyCell, playerCell);
            _lastPlannedPlayerCell = playerCell;
        }

        if (_currentPath == null || _pathIndex >= _currentPath.Count)
        {
            // No path exists right now (or it's fully walked) — hold still. The next attempt
            // happens automatically once the player's grid cell changes again.
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 waypointWorld = CellCenter(_currentPath[_pathIndex]);
        Vector2 toWaypoint = waypointWorld - _rb.position;

        if (toWaypoint.magnitude <= _waypointReachedDistance)
        {
            _pathIndex++;
            if (_pathIndex >= _currentPath.Count)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            waypointWorld = CellCenter(_currentPath[_pathIndex]);
            toWaypoint = waypointWorld - _rb.position;
        }

        _rb.linearVelocity = toWaypoint.normalized * _moveSpeed;
    }

    private void RecomputePath(Vector2Int enemyCell, Vector2Int playerCell)
    {
        _pathIndex = 0;

        Room? ownerRoomData = _roomGuard.OwnerRoomData;
        if (ownerRoomData == null || DungeonManager.Instance == null)
        {
            _currentPath = null;
            return;
        }

        Room room = ownerRoomData.Value;
        Vector2Int boundsMin = room.GridPos;
        Vector2Int boundsMax = new Vector2Int(room.GridPos.x + room.Width, room.GridPos.y + room.Height);

        _currentPath = EnemyPathfinder.FindPath(enemyCell, playerCell, boundsMin, boundsMax, IsCellWalkable);
    }

    /// <summary>Walkable = plain Floor only. Void cells, blocking obstacles (rocks), AND walkable
    /// hazard obstacles (fire, spikes...) are all treated as impassable for pathing purposes — a
    /// grounded enemy routes around a hazard instead of just shrugging off the damage the way it
    /// currently can via EnemyHazardDetector.</summary>
    private static bool IsCellWalkable(Vector2Int cell) =>
        DungeonManager.Instance.GetCellState(cell) == CellState.Floor;

    private static Vector2 CellCenter(Vector2Int cell) => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

    private void ClearPath()
    {
        _currentPath = null;
        _pathIndex = 0;
        _lastPlannedPlayerCell = null;
    }

    /// <summary>Called by EnemyRoomGuard on reset (player left the room without clearing it) —
    /// clears the planned path so a fresh one is computed next time this enemy starts chasing,
    /// instead of resuming a path planned from its pre-reset position.</summary>
    public void ResetEnemyState() => ClearPath();
}