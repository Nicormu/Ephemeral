using System.Collections;
using UnityEngine;
using TMPro;

public class LoadingTextAnimator : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Animation Settings")]
    [SerializeField] private string baseText = "Loading";
    [SerializeField] private float changeInterval = 0.5f;

    private int dotCount = 0;

    void OnEnable()
    {
        // Start the loop when the loading screen becomes active
        StartCoroutine(AnimateDots());
    }

    void OnDisable()
    {
        // Stop the loop when the loading screen is hidden
        StopAllCoroutines();
    }

    IEnumerator AnimateDots()
    {
        while (true)
        {
            // Build the string with the appropriate number of dots
            string dots = new string('.', dotCount + 1);
            
            if (loadingText != null)
            {
                loadingText.text = baseText + dots;
            }
            
            dotCount = (dotCount + 1) % 3;

            yield return new WaitForSeconds(changeInterval);
        }
    }
}