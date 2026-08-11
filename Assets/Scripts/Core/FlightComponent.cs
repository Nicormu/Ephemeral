using UnityEngine;

/// <summary>
/// Shared flight state for ANY entity — Player or Enemy — that can fly. A single toggleable
/// component instead of separate Player/Enemy wrappers (unlike HealthComponent's Player/Enemy
/// wrappers, "can this entity fly right now" needs no entity-specific behavior, just a bool and
/// a Layer swap), so both share this exact class.
///
/// Also keeps this GameObject's Physics2D Layer in sync with IsFlying: while flying, it switches
/// to _flyingLayerName (which should have collision with the "Obstacle" layer disabled in the
/// Physics2D Collision Matrix — see Project Settings > Physics 2D), so blocking obstacles (rocks)
/// are physically passed through automatically, with zero other script needing to touch Layer.
/// Switches back to the entity's original layer the moment flight turns off.
///
/// Hazard/Void immunity (see PlayerHazardDetector) is handled separately, by code that reads
/// IsFlying — NOT by physics layers, because that needs PER-OBSTACLE-TYPE granularity (e.g.
/// Fire should still damage a flying entity, Spikes shouldn't — see ObstacleType.IgnoredByFlyingEntities).
/// </summary>
public class FlightComponent : MonoBehaviour
{
    [Tooltip("If true, this entity starts already flying (e.g. a bat enemy that's always airborne). Leave false for entities that need to unlock/toggle flight at runtime (e.g. the player picking up a flight power-up) — call SetFlying(true) from that pickup's script instead.")]
    [SerializeField] private bool _startsFlying = false;

    [Tooltip("Physics2D Layer to switch this GameObject to while flying. Should have collision with the 'Obstacle' layer UNCHECKED in the Physics2D Collision Matrix, and collision with your wall layer left CHECKED so flying entities still stay contained inside rooms.")]
    [SerializeField] private string _flyingLayerName = "FlyingEntity";

    private int _groundedLayer;
    private int _flyingLayer;
    private bool _isFlying;

    /// <summary>Whether this entity is currently flying. Read by hazard-detection code
    /// (PlayerHazardDetector today, a future EnemyHazardDetector later) to decide whether
    /// Void-fall damage and per-ObstacleType hazard damage apply.</summary>
    public bool IsFlying => _isFlying;

    private void Awake()
    {
        _groundedLayer = gameObject.layer;
        _flyingLayer = _groundedLayer;

        if (!string.IsNullOrEmpty(_flyingLayerName))
        {
            int resolved = LayerMask.NameToLayer(_flyingLayerName);
            if (resolved < 0)
                Debug.LogWarning($"[FlightComponent] '{name}': Layer '{_flyingLayerName}' doesn't exist — create it under Project Settings > Tags and Layers. Flying won't change this GameObject's collision until that's fixed.");
            else
                _flyingLayer = resolved;
        }

        SetFlying(_startsFlying);
    }

    /// <summary>Turns flight on/off — swaps the GameObject's Layer to match, so physical
    /// collision with blocking obstacles (rocks) changes immediately. Call this from a power-up
    /// pickup, a status-effect timer, etc.</summary>
    public void SetFlying(bool value)
    {
        _isFlying = value;
        gameObject.layer = value ? _flyingLayer : _groundedLayer;
    }
}