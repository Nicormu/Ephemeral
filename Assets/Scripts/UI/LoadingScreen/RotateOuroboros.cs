using UnityEngine;

public class RotateOuroboros : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees per second. Negative rotates clockwise, positive rotates counter-clockwise.")]
    [SerializeField] private float rotationSpeed = -150f; 

    private RectTransform rectTransform;

    void Start()
    {
        // Cache the RectTransform component for UI performance
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (rectTransform != null)
        {
            // Rotate around the local Z-axis
            rectTransform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
    }
}