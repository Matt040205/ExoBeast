using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TopDownCameraManager : MonoBehaviour
{
    public static TopDownCameraManager Instance;

    [Header("Câmeras (Cinemachine)")]
    [Tooltip("Buscada automaticamente via código (CM_Normal).")]
    public CinemachineCamera cameraPrincipal;
    public CinemachineCamera buildCamera;

    [Header("Post-Processing (URP)")]
    public Volume globalVolume;
    private DepthOfField depthOfField;
    private ColorAdjustments colorAdjustments;
    public float tempoDeTransicao = 0.4f;

    // (Campos removidos para evitar retenção de referencias de rotas na hierarquia)

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
            {
                colorAdjustments.saturation.value = 0f;
            }
        }
    }

    public void SetCameraTarget(Transform localPlayerTransform)
    {
        if (buildCamera != null)
        {
            buildCamera.Follow = localPlayerTransform;
        }
    }

    void Update()
    {
        // Auto-conectar a câmera do jogador dinamicamente se estiver nula
        if (cameraPrincipal == null)
        {
            LocalizarCameraDoPlayer();
        }
    }

    private void LocalizarCameraDoPlayer()
    {
        // Busca todas as câmeras do Cinemachine 3 na cena ativas
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (CinemachineCamera cam in cameras)
        {
            // O nome da câmera principal nas imagens é CM_Normal
            if (cam.gameObject.name.Contains("CM_Normal"))
            {
                cameraPrincipal = cam;
                break;
            }
        }
    }

    // Chamado agora apenas pelo BuildManager para evitar Double-Toggling
    public void ToggleTopDownView(bool state)
    {
        visaoTopDownAtiva = state;
        
        ExecutarTransicaoJuice(state);
            
        UnityEngine.Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        UnityEngine.Cursor.visible = state;

        if (depthOfField != null)
        {
            depthOfField.active = !state;
        }
    }

    private void ExecutarTransicaoJuice(bool ativarTopDown)
    {
        // Problema corrigido: A câmera do player (Principal) agora mantém sua prioridade natural ditada pelo CameraController (10 ou 15 na mira).
        // A buildCamera "rouba" ativamente o Cinemachine subindo para 20 quando ativada, ou desce a 0 para devolver gentilmente com mix/blend.
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
        {
            StopCoroutine(coroutineDeTransicao);
        }
        coroutineDeTransicao = StartCoroutine(AnimarPostProcessing(ativarTopDown));
    }

    private Camera GetActiveCamera()
    {
        // 1. Prioridade Absoluta: CinemachineBrain ATIVO (Garante ser o jogador local, ignorando clones remotos desligados)
        CinemachineBrain[] brains = FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var b in brains)
        {
            if (b.isActiveAndEnabled)
            {
                Camera brainCam = b.GetComponent<Camera>();
                if (brainCam != null && brainCam.isActiveAndEnabled) return brainCam;
            }
        }

        // 2. Fallback
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;

        return FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
    }

    private void AlternarRotas(bool ativar)
    {
        int layerIndex = LayerMask.NameToLayer("Trilhas");
        
        if (layerIndex == -1)
        {
            Debug.LogWarning("Layer 'Trilhas' não foi encontrada! Certifique-se de criá-la nas configurações do Unity.");
            return;
        }

        int count = 0;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == null) continue;

            if (ativar)
            {
                cam.cullingMask |= (1 << layerIndex);
            }
            else
            {
                cam.cullingMask &= ~(1 << layerIndex);
            }
            count++;
        }

        Debug.Log($"[TopDownCamera] Máscara Trilhas ({(ativar ? "Ligada" : "Desligada")}) aplicada em {count} câmeras ativas. Mascara={LayerMask.GetMask("Trilhas")}.");
    }

    private IEnumerator AnimarPostProcessing(bool paraTopDown)
    {
        if (colorAdjustments == null) yield break;

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