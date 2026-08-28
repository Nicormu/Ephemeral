using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private GameObject loadingScreenCanvas;

    private bool _isLoading;

    public bool IsLoading => _isLoading;

    public void LoadNewScene(string sceneName)
    {
        if (_isLoading)
        {
            Debug.LogWarning($"[SceneLoader] Already loading — ignoring call to LoadNewScene(\"{sceneName}\")");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] LoadNewScene called with an empty or null scene name");
            return;
        }

        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        _isLoading = true;

        // Validate the canvas reference before doing anything.
        if (loadingScreenCanvas == null)
        {
            Debug.LogError("[SceneLoader] loadingScreenCanvas is not assigned! "
                + "Drag the LoadingScreen Canvas GameObject into the SceneLoader's inspector.");
            _isLoading = false;
            yield break;
        }

        loadingScreenCanvas.SetActive(true);

        // Start loading the scene in the background.
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        if (operation.isDone || operation.progress >= 0.9f)
        {
            // Scene is already fully loaded or is in the process of loading and has reached 90% progress.
            // Just set activation to true so it becomes playable immediately.
            operation.allowSceneActivation = true;
            loadingScreenCanvas.SetActive(false);
            _isLoading = false;
            yield break;
        }

        // Prevent the scene from instantly activating when it finishes loading.
        operation.allowSceneActivation = false;

        // Keep updating while the scene is still loading.
        int lastProgress = 0;
        float timeout = 120f; // safety net — give up after 2 minutes to avoid infinite loops.
        float elapsed = 0f;

        while (operation.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogError($"[SceneLoader] Timeout after {timeout}s waiting for \"{sceneName}\" "
                    + $"to reach 0.9 progress (current: {operation.progress:F2}). "
                    + "Is the scene in Build Settings?");
                break; // give up — canvas will be cleaned up below via yield return null.
            }

            int currentProgress = (int)(operation.progress * 100);
            if (currentProgress != lastProgress && currentProgress % 10 == 0)
            {
                Debug.Log($"[SceneLoader] Loading: {currentProgress}%");
                lastProgress = currentProgress;
            }

            yield return null;
        }

        // Activate the loaded scene.
        if (operation != null)
            operation.allowSceneActivation = true;

        yield return null; // Allow one frame for Unity to settle the transition.
    }
}