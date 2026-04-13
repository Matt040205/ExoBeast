using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// -- VolumeManager ---------------------------------------
/// Gerencia os volumes de Áudio (Master, Música, SFX) via Sliders e Botões.
/// Sincroniza com as Busses do FMOD e salva em PlayerPrefs.
/// --------------------------------------------------------
/// </summary>
public class VolumeManager : MonoBehaviour
{
    [Header("Sliders da UI")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Botões de Mute/Ativar")]
    public Button masterButton;
    public Button musicButton;
    public Button sfxButton;

    [Header("Caminhos dos Barramentos FMOD")]
    public string masterBusPath = "bus:/";
    public string musicBusPath = "bus:/Music";
    public string sfxBusPath = "bus:/SFX";

    private Bus masterBus;
    private Bus musicBus;
    private Bus sfxBus;

    private bool masterMuted;
    private bool musicMuted;
    private bool sfxMuted;

    void Awake()
    {
        masterBus = RuntimeManager.GetBus(masterBusPath);
        musicBus = RuntimeManager.GetBus(musicBusPath);
        sfxBus = RuntimeManager.GetBus(sfxBusPath);
    }

    void Start()
    {
        InitializeVolumes();
    }

    #region Inicialização

    private void InitializeVolumes()
    {
        float masterVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);

        masterMuted = PlayerPrefs.GetInt("MasterMute", 0) == 1;
        musicMuted = PlayerPrefs.GetInt("MusicMute", 0) == 1;
        sfxMuted = PlayerPrefs.GetInt("SFXMute", 0) == 1;

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

        if (masterButton != null) masterButton.onClick.AddListener(ToggleMasterMute);
        if (musicButton != null) musicButton.onClick.AddListener(ToggleMusicMute);
        if (sfxButton != null) sfxButton.onClick.AddListener(ToggleSFXMute);

        UpdateAllBusses();
    }

    #endregion

    #region Configurações de Volume

    public void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat("MasterVolume", value);
        if (!masterMuted) masterBus.setVolume(value);
    }

    public void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        if (!musicMuted) musicBus.setVolume(value);
    }

    public void SetSFXVolume(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        if (!sfxMuted) sfxBus.setVolume(value);
        
        // Garante que o SFX seja atualizado no barramento
        sfxBus.setVolume(sfxMuted ? 0 : value);
    }

    #endregion

    #region Configurações de Mute (Botões)

    public void ToggleMasterMute()
    {
        masterMuted = !masterMuted;
        PlayerPrefs.SetInt("MasterMute", masterMuted ? 1 : 0);
        UpdateAllBusses();
    }

    public void ToggleMusicMute()
    {
        musicMuted = !musicMuted;
        PlayerPrefs.SetInt("MusicMute", musicMuted ? 1 : 0);
        UpdateAllBusses();
    }

    public void ToggleSFXMute()
    {
        sfxMuted = !sfxMuted;
        PlayerPrefs.SetInt("SFXMute", sfxMuted ? 1 : 0);
        UpdateAllBusses();
    }

    private void UpdateAllBusses()
    {
        float masterVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);

        masterBus.setVolume(masterMuted ? 0 : masterVol);
        musicBus.setVolume(musicMuted ? 0 : musicVol);
        sfxBus.setVolume(sfxMuted ? 0 : sfxVol);

        // Feedback visual simples pode ser adicionado aqui se houver imagens nos botões
    }

    #endregion
}
