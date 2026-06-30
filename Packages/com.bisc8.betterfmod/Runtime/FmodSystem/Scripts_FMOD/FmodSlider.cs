#if FMOD_PRESENT
using FMOD.Studio;
using FMODUnity;
using System;
using UnityEngine;
using UnityEngine.UI;

public class FmodSlider : MonoBehaviour
{
    [Header("Mains Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private Bus masterBus;
    private Bus musicBus;
    private Bus sfxBus;

    public BusSlider[] otherSliders;

    [Serializable]
    public class BusSlider
    {
        [Tooltip("This name will be used in PlayerPrefs. Use uppercase letters at the beginning of each word.")]
        public string name;

        public string busPath;
        public Slider slider;

        [HideInInspector] public Bus bus;
    }

    void Start()
    {
        ResetPrefs();

        // Main buses
        TrySetupSlider(masterSlider, "bus:/Master", "Master", out masterBus);
        TrySetupSlider(musicSlider, "bus:/Master/Music", "Music", out musicBus);
        TrySetupSlider(sfxSlider, "bus:/Master/SFX", "SFX", out sfxBus);

        // Others sliders
        foreach (BusSlider busSlider in otherSliders)
        {
            TrySetupSlider(busSlider.slider, busSlider.busPath, busSlider.name, out busSlider.bus);
        }
    }

    bool TrySetupSlider(Slider slider, string busPath, string saveName, out Bus bus)
    {
        bus = default;

        if (slider == null || string.IsNullOrEmpty(busPath))
            return false;

        try
        {
            bus = RuntimeManager.GetBus(busPath);
        }
        catch (BusNotFoundException)
        {
            if (!FmodMultiplayerSettings.MultiplayerModeEnabled)
                Debug.LogWarning("[FMOD] Bus not found: " + busPath);

            return false;
        }

        SetupSlider(slider, bus, saveName);
        return true;
    }

    void SetupSlider(Slider slider, Bus bus, string saveName)
    {
        SetPrefs(slider, bus, saveName);

        slider.onValueChanged.AddListener((value) =>
        {
            SetVolume(bus, value);

            PlayerPrefs.SetFloat(saveName, value);
            PlayerPrefs.Save();
        });
    }

    void SetVolume(Bus bus, float value)
    {
        bus.setVolume(value);
    }

    void SetPrefs(Slider slider, Bus bus, string saveName)
    {
        float savedVolume = PlayerPrefs.GetFloat(saveName, 0.5f);

        slider.value = savedVolume;

        bus.setVolume(savedVolume);
    }

    void ResetPrefs()
    {
#if UNITY_EDITOR

        PlayerPrefs.DeleteKey("Master");
        PlayerPrefs.DeleteKey("Music");
        PlayerPrefs.DeleteKey("SFX");

        foreach (BusSlider busSlider in otherSliders)
        {
            PlayerPrefs.DeleteKey(busSlider.name);
        }

        PlayerPrefs.Save();

#endif
    }
}
#endif
