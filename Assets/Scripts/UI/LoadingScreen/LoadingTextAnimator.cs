using System.Collections;
using UnityEngine;
using TMPro; // Delete this line if you are using Legacy Text
using UnityEngine.UI; // Uncomment this line if you are using Legacy Text

public class LoadingTextAnimator : MonoBehaviour
{
    [Header("UI Reference")]
    // If using Legacy Text, change 'TextMeshProUGUI' to 'Text'
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
            // Build the string: e.g., "Loading." -> "Loading.." -> "Loading..."
            string dots = new string('.', dotCount + 1);
            
            if (loadingText != null)
            {
                loadingText.text = baseText + dots;
            }

            // Cycle dotCount between 0, 1, and 2 (for 1, 2, and 3 dots)
            dotCount = (dotCount + 1) % 3;

            yield return new WaitForSeconds(changeInterval);
        }
    }
}