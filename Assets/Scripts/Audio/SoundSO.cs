using UnityEngine;

/// <summary>
/// One reusable sound (SFX or music track). If several clips are assigned, one is picked at
/// random each time it plays, so repeated sounds (hits, steps) don't feel identical.
/// </summary>
[CreateAssetMenu(fileName = "NewSound", menuName = "Audio/Sound")]
public class SoundSO : ScriptableObject
{
    [Tooltip("Clip variants. One is chosen at random per play. Music usually has just one.")]
    public AudioClip[] Clips;

    [Range(0f, 1f)] public float Volume = 1f;

    [Tooltip("Random pitch range (min, max) applied per play. (1,1) = no variation. Try (0.95, 1.05) for SFX.")]
    public Vector2 PitchRange = Vector2.one;

    [Tooltip("Minimum seconds between two plays of THIS sound. Requests that arrive sooner are ignored. Use ~0.05-0.1 for sounds that many objects trigger in the same frame (e.g. doors opening together). 0 = no limit.")]
    [Min(0f)] public float MinInterval = 0f;

    public AudioClip GetRandomClip()
    {
        if (Clips == null || Clips.Length == 0) return null;
        return Clips[Random.Range(0, Clips.Length)];
    }

    public float GetRandomPitch() => Random.Range(PitchRange.x, PitchRange.y);
}