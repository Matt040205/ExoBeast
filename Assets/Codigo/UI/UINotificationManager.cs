using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Netcode;

/// <summary>
/// ── UINotificationManager ──────────────────────────────────
/// Gerencia os textos de alerta de horda globalmente.
/// Escuta os EnemyEvents locais no Servidor e repassa aos Clientes.
/// ───────────────────────────────────────────────────────────
/// </summary>
public class UINotificationManager : NetworkBehaviour
{
    public static UINotificationManager Instance { get; private set; }

    [Header("UI Elementos")]
    [Tooltip("Arraste o TextMeshProUGUI da sua HUD de Ingame aqui.")]
    public TextMeshProUGUI notificationText;
    
    [Header("Tempos")]
    public float fadeDuration = 0.5f;
    public float displayDuration = 3f;

    private Coroutine activeFadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        if (notificationText != null)
        {
            Color c = notificationText.color;
            c.a = 0;
            notificationText.color = c;
            notificationText.text = "";
        }

        EnemyEvents.OnEnemySpawned += TriggerSpawnNotification;
        EnemyEvents.OnEnemyHalfway += TriggerHalfwayNotification;
        EnemyEvents.OnEnemyReachedBase += TriggerBaseNotification;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        EnemyEvents.OnEnemySpawned -= TriggerSpawnNotification;
        EnemyEvents.OnEnemyHalfway -= TriggerHalfwayNotification;
        EnemyEvents.OnEnemyReachedBase -= TriggerBaseNotification;
    }

    // ════════════════════════════════════════════════════
    //  GATILHOS DOS OBSERVERS (Rodam no Servidor/Host/Single)
    // ════════════════════════════════════════════════════

    private void TriggerSpawnNotification(int pathIndex)
    {
        if (IsServer) NotifyUIClientRpc($"Inimigos nascendo [Caminho {pathIndex}]", Color.yellow);
        else if (IsLocalFallback()) ShowNotificationLocal($"Inimigos nascendo [Caminho {pathIndex}]", Color.yellow);
    }

    private void TriggerHalfwayNotification(int pathIndex)
    {
        // Laranja vibrante
        Color orangeColor = new Color(1f, 0.647f, 0f); 
        
        if (IsServer) NotifyUIClientRpc($"Inimigos na metade do caminho [Caminho {pathIndex}]", orangeColor);
        else if (IsLocalFallback()) ShowNotificationLocal($"Inimigos na metade do caminho [Caminho {pathIndex}]", orangeColor);
    }

    private void TriggerBaseNotification()
    {
        if (IsServer) NotifyUIClientRpc("Inimigos atingiram a base", Color.red);
        else if (IsLocalFallback()) ShowNotificationLocal("Inimigos atingiram a base", Color.red);
    }

    private bool IsLocalFallback()
    {
        return HordeManager.Instance != null && HordeManager.Instance.IsLocalMode;
    }

    // ════════════════════════════════════════════════════
    //  REDE E VISUAL (Rodam localmente em CADA tela)
    // ════════════════════════════════════════════════════

    [ClientRpc]
    private void NotifyUIClientRpc(string message, Color messageColor)
    {
        ShowNotificationLocal(message, messageColor);
    }

    private void ShowNotificationLocal(string message, Color messageColor)
    {
        if (notificationText == null) return;

        if (activeFadeRoutine != null) StopCoroutine(activeFadeRoutine);
        activeFadeRoutine = StartCoroutine(FadeRoutine(message, messageColor));
    }

    private IEnumerator FadeRoutine(string message, Color targetColor)
    {
        notificationText.text = message;
        
        // Cor base no alfa 0
        Color color = targetColor;
        color.a = 0f;
        notificationText.color = color;

        // FADE IN
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            notificationText.color = color;
            yield return null;
        }
        color.a = 1f;
        notificationText.color = color;

        // Leitura em tela
        yield return new WaitForSeconds(displayDuration);

        // FADE OUT
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            notificationText.color = color;
            yield return null;
        }

        color.a = 0f;
        notificationText.color = color;
        notificationText.text = "";
    }
}
