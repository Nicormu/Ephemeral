using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Music and SFX volume sliders. On Start, reads the saved values from MusicManager /
/// SFXManager (which load them from PlayerPrefs) and moves the sliders to match, using
/// SetValueWithoutNotify so initializing doesn't trigger SetVolume. Afterwards, slider changes
/// are forwarded to the managers, which convert 0..1 to decibels for the Mixer and save to
/// PlayerPrefs.
///
/// Start (not OnEnable) is used on purpose: UIPopupPanel never deactivates its GameObject, so
/// OnEnable runs once at scene load, possibly before the managers' Awake. Start always runs
/// after every Awake, so the managers are guaranteed to exist by then.
///
/// Open/close is NOT handled here — hook the Settings button to UIPopupPanel.Show() and the
/// close/back button to UIPopupPanel.Hide().
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;

    private void Start()
    {
        if (_musicSlider != null && MusicManager.Instance != null)
            _musicSlider.SetValueWithoutNotify(MusicManager.Instance.GetVolume());

        if (_sfxSlider != null && SFXManager.Instance != null)
            _sfxSlider.SetValueWithoutNotify(SFXManager.Instance.GetVolume());
    }

    private void OnEnable()
    {
        if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnDisable()
    {
        if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
    }

    private void OnMusicChanged(float value) => MusicManager.Instance?.SetVolume(value);
    private void OnSfxChanged(float value) => SFXManager.Instance?.SetVolume(value);
}