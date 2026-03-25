using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using FMOD.Studio;

public class VolumeManager : MonoBehaviour
{
    [Header("Sliders da UI")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Caminhos dos Barramentos FMOD (Verifique no FMOD Studio)")]
    // O padrão do FMOD é "bus:/". Se você criou grupos, adicione os nomes.
    // Exemplo: "bus:/Music", "bus:/SFX"
    public string masterBusPath = "bus:/";
    public string musicBusPath = "bus:/Music";
    public string sfxBusPath = "bus:/SFX";

    private Bus masterBus;
    private Bus musicBus;
    private Bus sfxBus;

    void Awake()
    {
        // Pega as referências dos canais de áudio do FMOD
        masterBus = RuntimeManager.GetBus(masterBusPath);
        musicBus = RuntimeManager.GetBus(musicBusPath);
        sfxBus = RuntimeManager.GetBus(sfxBusPath);
    }

    void Start()
    {
        // Carrega os valores salvos (ou usa 1.0 se for a primeira vez)
        float masterVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);

        // Configura os Sliders visuais
        if (masterSlider != null)
        {
            masterSlider.value = masterVol;
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (musicSlider != null)
        {
            musicSlider.value = musicVol;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = sfxVol;
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        masterBus.setVolume(masterVol);
        musicBus.setVolume(musicVol);
        sfxBus.setVolume(sfxVol);
    }

    public void SetMasterVolume(float value)
    {
        masterBus.setVolume(value);
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    public void SetMusicVolume(float value)
    {
        musicBus.setVolume(value);
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    public void SetSFXVolume(float value)
    {
        sfxBus.setVolume(value);
        PlayerPrefs.SetFloat("SFXVolume", value);
    }
}