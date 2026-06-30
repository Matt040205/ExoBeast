#if FMOD_PRESENT
using UnityEngine;

public class FmodMultiplayerSettings : MonoBehaviour
{
    [SerializeField] private bool isMultiplayer;

    public bool IsMultiplayer => isMultiplayer;

    public static bool MultiplayerModeEnabled { get; private set; }

    private static FmodMultiplayerSettings activeSettings;

    private void Awake()
    {
        activeSettings = this;
        MultiplayerModeEnabled = isMultiplayer;
    }

    private void OnDestroy()
    {
        if (activeSettings != this)
            return;

        activeSettings = null;
        RefreshActiveSettings();
    }

    private static void RefreshActiveSettings()
    {
        foreach (FmodMultiplayerSettings settings in FindObjectsByType<FmodMultiplayerSettings>(FindObjectsSortMode.None))
        {
            if (settings == activeSettings || !settings.isActiveAndEnabled)
                continue;

            activeSettings = settings;
            MultiplayerModeEnabled = settings.isMultiplayer;
            return;
        }

        MultiplayerModeEnabled = false;
    }
}
#endif
