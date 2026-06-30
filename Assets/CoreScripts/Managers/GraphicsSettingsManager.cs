using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// -- GraphicsSettingsManager ----------------------------------
/// Gerencia as resoluções do monitor e modo de tela cheia via Dropdown e Botão.
/// Permite carregar e salvar as preferências gráficas do usuário.
/// ------------------------------------------------------------
/// </summary>
public class GraphicsSettingsManager : MonoBehaviour
{
    public TMP_Dropdown resolutionDropdown;
    public Button fullscreenButton;

    private Resolution[] resolutions;

    void Start()
    {
        InitializeResolutions();
    }

    #region Inicialização

    private void InitializeResolutions()
    {
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height + " @ " + (int)(resolutions[i].refreshRateRatio.numerator / resolutions[i].refreshRateRatio.denominator) + "Hz";
            options.Add(option);

            if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);

        int savedResIndex = PlayerPrefs.GetInt("ResolutionIndex", currentResolutionIndex);
        if (savedResIndex >= resolutions.Length) savedResIndex = resolutions.Length - 1;

        resolutionDropdown.value = savedResIndex;
        resolutionDropdown.RefreshShownValue();

        if (fullscreenButton != null) fullscreenButton.onClick.AddListener(ToggleFullscreen);
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    #endregion

    #region Configurações de Tela

    public void SetResolution(int index)
    {
        if (index < 0 || index >= resolutions.Length) return;

        Resolution res = resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        PlayerPrefs.SetInt("ResolutionIndex", index);
    }

    public void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
        Debug.Log("[GraphicsSettings] Fullscreen alternado para: " + Screen.fullScreen);
    }

    #endregion
}
