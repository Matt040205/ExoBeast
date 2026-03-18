using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TopDownCameraManager : MonoBehaviour
{
    public static TopDownCameraManager Instance;

    public CinemachineCamera buildCamera;
    public Volume globalVolume;
    private DepthOfField depthOfField;

    private const int PriorityBuild = 20;
    private const int PriorityInactive = 0;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        buildCamera.Priority.Value = PriorityInactive;

        if (globalVolume != null)
        {
            globalVolume.profile.TryGet(out depthOfField);
        }
    }

    public void ToggleTopDownView(bool state)
    {
        buildCamera.Priority.Value = state ? PriorityBuild : PriorityInactive;
        UnityEngine.Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        UnityEngine.Cursor.visible = state;

        if (depthOfField != null)
        {
            depthOfField.active = !state;
        }
    }
}