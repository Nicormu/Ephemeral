using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private GameObject loadingScreenCanvas; // Drag your LoadingScreen GameObject here

    // Call this function to start loading a scene (e.g., from a main menu button)
    public void LoadNewScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        // 1. Show the loading screen
        loadingScreenCanvas.SetActive(true);

        // 2. Start loading the scene in the background
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        // Prevent the scene from instantly activating when it finishes loading
        // (Great if you want to wait for a "Press any key to continue" prompt)
        operation.allowSceneActivation = false; 

        // 3. Keep updating while the scene is still loading
        // (Unity async progress stops at 0.9f when allowSceneActivation is false)
        while (operation.progress < 0.9f)
        {
            // You can use operation.progress here to update a loading bar!
            yield return null; 
        }

        // Optional: Wait a tiny fraction of a second so the player actually sees the screen
        yield return new WaitForSeconds(0.5f);

        // 4. Loading is finished! Activate the new scene
        operation.allowSceneActivation = true;

        // 5. Hide the loading screen (or let the new scene handle closing it)
        loadingScreenCanvas.SetActive(false);
    }
}