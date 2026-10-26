using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Plays sound effects through a small pool of AudioSources so several can overlap, each with
/// its own volume/pitch. Persistent across scenes (lives on the root "AudioSystem" object).
/// </summary>
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    private const string PrefsKey = "SFXVolume";

    [SerializeField] private AudioMixerGroup _outputGroup;

    [Tooltip("Mixer that owns the exposed volume parameter below.")]
    [SerializeField] private AudioMixer _mixer;

    [Tooltip("Name of the exposed Mixer parameter controlling the SFX group's volume.")]
    [SerializeField] private string _volumeParameter = "SFXVolume";

    [Tooltip("How many sounds can play at once. If all are busy, the next play steals the oldest.")]
    [SerializeField] private int _poolSize = 12;

    private AudioSource[] _pool;
    private int _nextIndex;

    // Last time each SoundSO actually played, used to enforce SoundSO.MinInterval.
    private readonly Dictionary<SoundSO, float> _lastPlayTime = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (transform.parent != null)
            Debug.LogWarning("[SFXManager] DontDestroyOnLoad only works on ROOT objects — move AudioSystem to the scene root.");
        DontDestroyOnLoad(gameObject);

        _pool = new AudioSource[Mathf.Max(1, _poolSize)];
        for (int i = 0; i < _pool.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = _outputGroup;
            _pool[i] = src;
        }
    }

    private void Start() => SetVolume(GetVolume()); // Start, not Awake: Mixer.SetFloat can be ignored this early

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Play(SoundSO sound)
    {
        if (sound == null) return;

        // Unscaled time so the limit still works if Time.timeScale is 0 (pause, death screen).
        if (sound.MinInterval > 0f)
        {
            float now = Time.unscaledTime;
            if (_lastPlayTime.TryGetValue(sound, out float last) && now - last < sound.MinInterval)
                return;
            _lastPlayTime[sound] = now;
        }

        AudioClip clip = sound.GetRandomClip();
        if (clip == null)
        {
            Debug.LogWarning($"[SFXManager] Sound '{sound.name}' has no clips assigned.");
            return;
        }

        AudioSource src = GetSource();
        src.clip = clip;
        src.volume = sound.Volume;
        src.pitch = sound.GetRandomPitch();
        src.Play();
    }

    /// <summary>Prefers a free source; if every one is busy, reuses the next in round-robin order.</summary>
    private AudioSource GetSource()
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            int idx = (_nextIndex + i) % _pool.Length;
            if (!_pool[idx].isPlaying)
            {
                _nextIndex = (idx + 1) % _pool.Length;
                return _pool[idx];
            }
        }

        AudioSource stolen = _pool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % _pool.Length;
        return stolen;
    }

    /// <summary>Slider value 0..1 (converted to decibels for the Mixer) and saved to PlayerPrefs.</summary>
    public void SetVolume(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        float db = value01 > 0.0001f ? Mathf.Log10(value01) * 20f : -80f;
        if (_mixer != null) _mixer.SetFloat(_volumeParameter, db);
        PlayerPrefs.SetFloat(PrefsKey, value01);
    }

    public float GetVolume() => PlayerPrefs.GetFloat(PrefsKey, 1f);
}