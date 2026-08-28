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
    private Coroutine _animateRoutine;

    void OnEnable()
    {
        if (_animateRoutine != null)
        {
            StopCoroutine(_animateRoutine);
            _animateRoutine = null;
        }

        _animateRoutine = StartCoroutine(AnimateDots());
    }

    void OnDisable()
    {
        // Explicitly stop only the dot animation coroutine — never use StopAllCoroutines()
        // because it kills ALL coroutines on this component, including unrelated ones.
        if (_animateRoutine != null)
        {
            StopCoroutine(_animateRoutine);
            _animateRoutine = null;
        }

        // Reset to 0 so the animation starts fresh when re-enabled (no dots visible).
        dotCount = 0;
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