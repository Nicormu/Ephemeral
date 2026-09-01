using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private GameObject loadingScreenCanvas;

    [Header("Curtain Transition")]
    [Tooltip("Lateral ouroboros curtain on THIS scene's Canvas Manager. Only Close() is used here — reopening is handled independently by the new scene's own curtain (Open On Start), since this object is destroyed the moment the new scene activates. Leave empty to skip the curtain entirely.")]
    [SerializeField] private LateralOuroborosCurtain _curtain;

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

        if (loadingScreenCanvas == null)
        {
            Debug.LogError("[SceneLoader] loadingScreenCanvas is not assigned! "
                + "Drag the LoadingScreen Canvas GameObject into the SceneLoader's inspector.");
            _isLoading = false;
            yield break;
        }

        loadingScreenCanvas.SetActive(true);

        bool curtainClosed = _curtain == null;
        if (_curtain != null)
            _curtain.Close(() => curtainClosed = true);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        operation.allowSceneActivation = false;

        int lastProgress = 0;
        float timeout = 120f;
        float elapsed = 0f;

        while (operation.progress < 0.9f || !curtainClosed)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogError($"[SceneLoader] Timeout after {timeout}s waiting for \"{sceneName}\" "
                    + $"to reach 0.9 progress (current: {operation.progress:F2}). "
                    + "Is the scene in Build Settings?");
                break;
            }

            int currentProgress = (int)(operation.progress * 100);
            if (currentProgress != lastProgress && currentProgress % 10 == 0)
            {
                Debug.Log($"[SceneLoader] Loading: {currentProgress}%");
                lastProgress = currentProgress;
            }

            yield return null;
        }

        // From here, this scene (and this coroutine's object) is about to be destroyed —
        // the new scene's own Loading Canvas curtain (Open On Start) takes over from here.
        operation.allowSceneActivation = true;
    }
}