using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TopDownCameraManager : MonoBehaviour
{
    public static TopDownCameraManager Instance;

    [Header("CÃ¢meras (Cinemachine)")]
    [Tooltip("Buscada automaticamente via cÃ³digo (CM_Normal).")]
    public CinemachineCamera cameraPrincipal;
    public CinemachineCamera buildCamera;

    [Header("Post-Processing (URP)")]
    public Volume globalVolume;
    private DepthOfField depthOfField;
    private ColorAdjustments colorAdjustments;
    public float tempoDeTransicao = 0.4f;

    private bool visaoTopDownAtiva = false;
    private Coroutine coroutineDeTransicao;

    private const int PriorityInactive = 0;
    private const int PriorityTopDown = 20;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        AlternarRotas(false);

        if (buildCamera != null)
            buildCamera.Priority.Value = PriorityInactive;

        if (globalVolume != null)
        {
            globalVolume.profile.TryGet(out depthOfField);

            if (globalVolume.profile.TryGet(out colorAdjustments))
                colorAdjustments.saturation.value = 0f;
        }
    }

    public void SetCameraTarget(Transform localPlayerTransform)
    {
        if (buildCamera != null)
            buildCamera.Follow = localPlayerTransform;
    }

    void Update()
    {
        if (cameraPrincipal == null)
            LocalizarCameraDoPlayer();
    }

    private void LocalizarCameraDoPlayer()
    {
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (CinemachineCamera cam in cameras)
        {
            if (cam != null && cam.gameObject.name.Contains("CM_Normal"))
            {
                cameraPrincipal = cam;
                break;
            }
        }
    }

    public void ToggleTopDownView(bool state)
    {
        visaoTopDownAtiva = state;
        ExecutarTransicaoJuice(state);

        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = state;

        if (depthOfField != null)
            depthOfField.active = !state;
    }

    private void ExecutarTransicaoJuice(bool ativarTopDown)
    {
        if (ativarTopDown)
        {
            if (buildCamera != null) buildCamera.Priority.Value = PriorityTopDown;
        }
        else
        {
            if (buildCamera != null) buildCamera.Priority.Value = PriorityInactive;
        }

        AlternarRotas(ativarTopDown);

        if (coroutineDeTransicao != null)
            StopCoroutine(coroutineDeTransicao);

        coroutineDeTransicao = StartCoroutine(AnimarPostProcessing(ativarTopDown));
    }

    private Camera GetActiveCamera()
    {
        CinemachineBrain[] brains = FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (CinemachineBrain brain in brains)
        {
            if (!brain.isActiveAndEnabled)
                continue;

            Camera brainCamera = brain.GetComponent<Camera>();
            if (brainCamera != null && brainCamera.isActiveAndEnabled)
                return brainCamera;
        }

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        return FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
    }

    private void AlternarRotas(bool ativar)
    {
        int layerIndex = LayerMask.NameToLayer("Trilhas");
        if (layerIndex == -1)
        {
            Debug.LogWarning("Layer 'Trilhas' nÃ£o foi encontrada! Certifique-se de criÃ¡-la nas configuraÃ§Ãµes do Unity.");
            return;
        }

        Camera activeCamera = GetActiveCamera();
        if (activeCamera == null)
            return;

        if (ativar)
            activeCamera.cullingMask |= (1 << layerIndex);
        else
            activeCamera.cullingMask &= ~(1 << layerIndex);

        Debug.Log($"[TopDownCamera] MÃ¡scara Trilhas ({(ativar ? "Ligada" : "Desligada")}) aplicada na cÃ¢mera local ativa.");
    }

    private IEnumerator AnimarPostProcessing(bool paraTopDown)
    {
        if (colorAdjustments == null)
            yield break;

        float tempoDecorrido = 0f;
        float saturacaoInicial = colorAdjustments.saturation.value;
        float saturacaoAlvo = paraTopDown ? -40f : 0f;

        while (tempoDecorrido < tempoDeTransicao)
        {
            tempoDecorrido += Time.deltaTime;
            float t = tempoDecorrido / tempoDeTransicao;
            t = t * t * (3f - 2f * t);

            colorAdjustments.saturation.value = Mathf.Lerp(saturacaoInicial, saturacaoAlvo, t);
            yield return null;
        }

        colorAdjustments.saturation.value = saturacaoAlvo;
    }
}
