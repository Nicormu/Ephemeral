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

    [Header("Rolling")]
    [Tooltip("Duration of the roll in seconds.")]
    [SerializeField] private float _rollDuration = 0.3f;

    [Tooltip("Speed during the roll (multiplier over base move speed).")]
    [SerializeField] private float _rollSpeedMultiplier = 2.5f;

    [Tooltip("Cooldown between rolls in seconds.")]
    [SerializeField] private float _rollCooldown = 0.5f;

    // -- component refs --
    private Rigidbody2D _rb;

    // -- state --
    private PlayerState _currentState;

    private float _lastRollTime;

    // -- direction the player is facing (cached to avoid normalizing every frame) --
    private Vector2 _currentDirection;

    // -- last known facing direction, used when no input is provided (e.g. at startup) --
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
        // Read input axes (WASD / arrow keys).
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(horizontal, vertical);

        // Clamp to unit length so diagonal movement isn't faster.
        if (input.magnitude > 1f)
            input.Normalize();

        // Update direction for later use by camera / UI.
        _currentDirection = input;

        if (input.sqrMagnitude < Mathf.Epsilon)
        {
            // No input received — idle.
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

    // private void HandleRolling()
    // {
    //     ...
    // }

    #endregion

    #region Teleport

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

    #endregion
}