using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.x

/// <summary>
/// ── CameraShakeManager ───────────────────────────────────
/// Captura disparos do JuiceEvents e injeta no Cinemachine.
/// Realiza a matemática Top-Down anulando o eixo Z para 
/// previnir enjoo ou distorção de enquadramento.
/// ─────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShakeManager : MonoBehaviour
{
    private CinemachineImpulseSource impulseSource;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable()
    {
        JuiceEvents.OnCameraShake += HandleDirectionalShake;
    }

    private void OnDisable()
    {
        JuiceEvents.OnCameraShake -= HandleDirectionalShake;
    }

    private void HandleDirectionalShake(Vector3 worldDirection, float amplitude, float frequency, float duration)
    {
        if (Camera.main == null || impulseSource == null) return;

        // Zera puramente o eixo Y no caso de vir do chão (para não tremer verticalmente falso)
        Vector3 rawDir = worldDirection.normalized;

        // Projeta no plano nativo da tela da Câmera (X e Y)
        Vector3 screenRight = Camera.main.transform.right;
        Vector3 screenUp = Camera.main.transform.up;

        float rightProj = Vector3.Dot(rawDir, screenRight);
        float upProj = Vector3.Dot(rawDir, screenUp);

        // Vector puro, limitando qualquer escape tridimensional no eixo Z
        Vector3 localImpulse = new Vector3(rightProj, upProj, 0f).normalized;

        // Altera propriedades dinâmicas do componente antes de disparar o tiro de impulso
        impulseSource.ImpulseDefinition.AmplitudeGain = amplitude;
        impulseSource.ImpulseDefinition.FrequencyGain = frequency;
        
        // Em Cinemachine 3.x, o envelope é acessado nativamente
        impulseSource.ImpulseDefinition.TimeEnvelope.SustainTime = duration;

        // O Vector3 Velocity indica a direção do baque projetada com nossa matemática
        impulseSource.GenerateImpulseWithVelocity(localImpulse);
    }
}
