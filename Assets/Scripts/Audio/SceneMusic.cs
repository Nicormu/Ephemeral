using UnityEngine;

/// <summary>
/// Place one of these in each scene to choose that scene's background music. On Start it asks
/// MusicManager to crossfade to the assigned track. If that track is already playing (e.g. the
/// same song continues across two scenes), nothing happens — MusicManager.Play ignores it.
/// </summary>
public class SceneMusic : MonoBehaviour
{
    [Tooltip("Track to play in this scene. Leave empty to FADE OUT whatever is playing (silent scene).")]
    [SerializeField] private SoundSO _music;

    [Tooltip("Crossfade seconds. -1 uses MusicManager's default.")]
    [SerializeField] private float _fadeDuration = -1f;

    private void Start()
    {
        if (MusicManager.Instance == null)
        {
            Debug.LogWarning("[SceneMusic] No MusicManager in the scene — add the AudioSystem prefab.");
            return;
        }

        MusicManager.Instance.Play(_music, _fadeDuration);
    }
}