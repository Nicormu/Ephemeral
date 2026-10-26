using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Plays one looping music track at a time, crossfading between tracks using two AudioSources.
/// Persistent across scenes (lives on the root "AudioSystem" object).
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    private const string PrefsKey = "MusicVolume";

    [SerializeField] private AudioMixerGroup _outputGroup;
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private string _volumeParameter = "MusicVolume";

    [Tooltip("Default crossfade seconds when Play() is called without one.")]
    [SerializeField] private float _defaultFadeDuration = 1f;

    private AudioSource _active;
    private AudioSource _inactive;
    private SoundSO _current;
    private Coroutine _fadeRoutine;

    public SoundSO CurrentTrack => _current;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (transform.parent != null)
            Debug.LogWarning("[MusicManager] DontDestroyOnLoad only works on ROOT objects — move AudioSystem to the scene root.");
        DontDestroyOnLoad(gameObject);

        _active = CreateSource();
        _inactive = CreateSource();
    }

    private void Start() => SetVolume(GetVolume());

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioSource CreateSource()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.volume = 0f;
        src.outputAudioMixerGroup = _outputGroup;
        return src;
    }

    /// <summary>Crossfades to this track. Does nothing if it's already the current one,
    /// so every scene can safely call Play() in its Start() without restarting the music.</summary>
    public void Play(SoundSO track, float fadeDuration = -1f)
    {
        if (track == null) { Stop(fadeDuration); return; }
        if (track == _current) return;

        AudioClip clip = track.GetRandomClip();
        if (clip == null)
        {
            Debug.LogWarning($"[MusicManager] Track '{track.name}' has no clips assigned.");
            return;
        }

        _current = track;
        if (fadeDuration < 0f) fadeDuration = _defaultFadeDuration;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

        (_active, _inactive) = (_inactive, _active); // the old active source becomes the one fading out
        _active.clip = clip;
        _active.volume = 0f;
        _active.Play();

        _fadeRoutine = StartCoroutine(Crossfade(_active, _inactive, track.Volume, fadeDuration));
    }

    public void Stop(float fadeDuration = -1f)
    {
        if (_current == null) return;
        _current = null;
        if (fadeDuration < 0f) fadeDuration = _defaultFadeDuration;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Crossfade(null, _active, 0f, fadeDuration));
    }

    /// <summary>Fades 'fadeIn' up to targetVolume while fading 'fadeOut' down to 0 (either can be null).
    /// Unscaled time, so it still works if Time.timeScale is 0 (death screen, pause).</summary>
    private IEnumerator Crossfade(AudioSource fadeIn, AudioSource fadeOut, float targetVolume, float duration)
    {
        float inStart = fadeIn != null ? fadeIn.volume : 0f;
        float outStart = fadeOut != null ? fadeOut.volume : 0f;
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (fadeIn != null) fadeIn.volume = Mathf.Lerp(inStart, targetVolume, t);
            if (fadeOut != null) fadeOut.volume = Mathf.Lerp(outStart, 0f, t);
            yield return null;
        }

        if (fadeIn != null) fadeIn.volume = targetVolume;
        if (fadeOut != null) { fadeOut.volume = 0f; fadeOut.Stop(); }
        _fadeRoutine = null;
    }

    public void SetVolume(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        float db = value01 > 0.0001f ? Mathf.Log10(value01) * 20f : -80f;
        if (_mixer != null) _mixer.SetFloat(_volumeParameter, db);
        PlayerPrefs.SetFloat(PrefsKey, value01);
    }

    public float GetVolume() => PlayerPrefs.GetFloat(PrefsKey, 1f);
}