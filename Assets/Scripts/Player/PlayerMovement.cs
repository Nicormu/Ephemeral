using UnityEngine;

/// <summary>
/// Handles player movement and rolling with state tracking.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    public static PlayerMovement Instance { get; private set; }

    [Header("Movement")]
    [Tooltip("Speed at which the player moves while walking.")]
    [SerializeField] private float _moveSpeed = 3f;

    //[Header("Rolling")]
    //[Tooltip("Duration of the roll in seconds.")]
    //[SerializeField] private float _rollDuration = 0.3f;

    [Tooltip("Speed during the roll (multiplier over base move speed).")]
    //[SerializeField] private float _rollSpeedMultiplier = 2.5f;

    //[Tooltip("Cooldown between rolls in seconds.")]
    //[SerializeField] private float _rollCooldown = 0.5f;

    // -- component refs --
    private Rigidbody2D _rb;
    private Collider2D _collider;

    // -- state --
    private PlayerState _currentState;
    private bool _inputEnabled = true;

    private float _lastRollTime;

    // -- direction the player is facing (cached to avoid normalizing every frame) --
    private Vector2 _currentDirection;

    private Vector2 _lastFacingDirection = Vector2.right;

    // -- properties --
    public PlayerState CurrentState => _currentState;
    public Vector2 Direction => _currentDirection;
    public bool IsRolling => _currentState == PlayerState.Rolling;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _currentState = PlayerState.Idle;
    }

    private void FixedUpdate()
    {
        switch (_currentState)
        {
            case PlayerState.Idle:
            case PlayerState.Moving:
                HandleMovement();
                break;

            case PlayerState.Rolling:
                // HandleRolling();
                break;
        }
    }

    private void Update()
    {
        // Rolling disabled:
        // TryStartRoll();

        // Record the last facing direction so a roll immediately after spawn
        // still goes somewhere sensible instead of defaulting to +X.
        if (_currentDirection.sqrMagnitude > Mathf.Epsilon)
            _lastFacingDirection = _currentDirection;
    }

    #region State management

    /// <summary>
    /// Transitions the player into a new state, performing cleanup / reset as needed.
    /// </summary>
    private void SetState(PlayerState newState)
    {
        if (_currentState == newState)
            return;

        // Clean up from the previous state.
        if (_currentState == PlayerState.Rolling)
            _rb.linearVelocity = Vector2.zero;

        _currentState = newState;
    }

    private void TryStartRoll()
    {
        // Rolling disabled
        return;

        /*
        // Don't allow rolling while already rolling.
        if (_currentState == PlayerState.Rolling)
            return;

        // Check cooldown (unscaled so pause / death screens don't freeze the timer).
        if (Time.unscaledTime - _lastRollTime < _rollCooldown)
            return;

        // Trigger on left-mouse / right-click, or the "Fire1" input axis (default: Q).
        if (!Input.GetButtonDown("Fire2"))
            return;

        StartRoll();
        */
    }

    private void StartRoll()
    {
        _lastRollTime = Time.time;
        SetState(PlayerState.Rolling);
    }

    #endregion

    private void OnDisable()
    {
        // Clean up singleton reference so stale references don't survive scene reloads.
        if (Instance == this) Instance = null;
    }

    #region Movement

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;

        if (_inputEnabled)
        {
            input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (input.magnitude > 1f)
                input.Normalize();
        }

        _currentDirection = input;

        if (input.sqrMagnitude < Mathf.Epsilon)
        {
            SetState(PlayerState.Idle);
            _rb.linearVelocity = Vector2.zero;
        }
        else
        {
            SetState(PlayerState.Moving);
            _rb.linearVelocity = input * _moveSpeed;
        }
    }

    #endregion

    #region Rolling

    // Re-enable when animations are ready.

    #endregion

    #region Teleport / External control

    /// <summary>
    /// Instantly moves the player to a new position and clears any residual velocity/roll state.
    /// Use this for spawn placement and hazard recovery instead of setting transform.position
    /// directly, so momentum from before the teleport doesn't leak into the new location.
    /// </summary>
    public void TeleportTo(Vector3 worldPosition)
    {
        transform.position = worldPosition;

        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        SetState(PlayerState.Idle);
    }

    /// <summary>
    /// Enables/disables player input control. Used by external systems that need to drive the
    /// player's transform directly for a moment — e.g. RoomCamera's door-crossing transition,
    /// which glides the player across the gap between rooms while the camera pans. Disabling
    /// stops both FixedUpdate movement and Update's facing-direction tracking (component is
    /// simply turned off), and zeroes residual velocity so the player doesn't drift once control
    /// is handed back.
    ///
    /// Also suspends the player's own Collider2D while disabled: during an externally-driven
    /// glide the destination is already a known-safe position, so physics shouldn't be able to
    /// snag the player on a closed/misaligned door collider (or anything else) along the way.
    /// Re-enabled the instant control is handed back.
    /// </summary>
    public void SetInputEnabled(bool value)
    {
        _inputEnabled = value;

        if (!value)
            _rb.linearVelocity = Vector2.zero;

        if (_collider != null)
            _collider.enabled = value;
    }
    #endregion
}