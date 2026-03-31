using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ── TopDownCameraManager ────────────────────────────
/// Gerencia a camera top-down usada no modo de construcao.
///
///  ▸ SetCameraTarget(): vincula buildCamera ao jogador local via OnNetworkSpawn
///  ▸ ToggleTopDownView(): alterna prioridade Cinemachine + cursor + DoF
///  ▸ Singleton de cena — nao eh NetworkBehaviour
/// ─────────────────────────────────────────────────────
/// </summary>
public class TopDownCameraManager : MonoBehaviour
{
    public static TopDownCameraManager Instance;

    public CinemachineCamera buildCamera;
    public Volume globalVolume;
    private DepthOfField depthOfField;

    private const int PriorityBuild = 20;
    private const int PriorityInactive = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // Apenas destrói o script duplicado, protegendo o resto do objeto!
            return;
        }
        Instance = this;
    }


    void Start()
    {
        if (buildCamera != null)
            buildCamera.Priority.Value = PriorityInactive;

        if (globalVolume != null)
        {
            globalVolume.profile.TryGet(out depthOfField);
        }
    }

    public void SetCameraTarget(Transform localPlayerTransform)
    {
        if (buildCamera != null)
        {
            buildCamera.Follow = localPlayerTransform;
        }
    }

    public void ToggleTopDownView(bool state)
    {
        if (buildCamera != null)
            buildCamera.Priority.Value = state ? PriorityBuild : PriorityInactive;
            
        UnityEngine.Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        UnityEngine.Cursor.visible = state;

        if (depthOfField != null)
        {
            depthOfField.active = !state;
        }
    }
}