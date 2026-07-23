using UnityEngine;

public class Exit : MonoBehaviour
{
    public void ExitGame()
    {
        Debug.Log("Exit Game requested");

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}