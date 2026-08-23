using UnityEngine;

/// <summary>
/// Shared flight state for ANY entity — Player or Enemy — that can fly. 
/// "can this entity fly right now" needs no entity-specific behavior, 
/// just a bool and a Layer swap), so both share this exact class.
///
/// Blocking obstacles (rocks) are physically passed through automatically, with zero other script needing to touch Layer.
///
/// Hazard/Void immunity is handled separately, see ObstacleType.IgnoredByFlyingEntities.
/// </summary>
public class FlightComponent : MonoBehaviour
{
    [Tooltip("Idk what to tell you, the name says it :p. If true, this entity will start the game flying. If false, it will start grounded.")]
    [SerializeField] private bool _startsFlying = false;

    [Tooltip("Physics2D Layer to switch this GameObject to while flying.")]
    [SerializeField] private string _flyingLayerName = "FlyingEntity";

    private int _groundedLayer;
    private int _flyingLayer;
    private bool _isFlying;

    /// <summary>
    /// Whether this entity is currently flying. 
    /// Read by hazard-detection code to decide whether Void-fall damage and per-ObstacleType hazard damage apply.
    /// </summary>
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

    /// <summary>
    /// Turns flight on/off — swaps the GameObject's Layer to match
    /// I could used it to make player fly in a future
    /// </summary>
    public void SetFlying(bool value)
    {
        _isFlying = value;
        gameObject.layer = value ? _flyingLayer : _groundedLayer;
    }
}