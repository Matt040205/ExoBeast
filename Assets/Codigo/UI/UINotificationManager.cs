using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// UINotificationManager
/// Gerencia os textos de alerta de horda globalmente.
/// Escuta os EnemyEvents locais no Servidor e repassa aos Clientes.
/// Conta com um sistema de Fila + Debounce para evitar Spam de Eventos.
/// </summary>
public class UINotificationManager : NetworkBehaviour
{
    public static UINotificationManager Instance { get; private set; }

    [Header("UI Elementos")]
    [Tooltip("Arraste o TextMeshProUGUI da sua HUD de Ingame aqui.")]
    public TextMeshProUGUI notificationText;
    
    [Header("Tempos de Exibicao")]
    public float fadeDuration = 0.5f;
    public float displayDuration = 2f;

    [Header("Controle de Spam (Debounce)")]
    [Tooltip("Tempo minimo em segundos antes que a mesma mensagem possa ser repetida.")]
    public float messageCooldown = 3f;

    private Coroutine queueCoroutine;
    
    // Fila de notificacoes para exibir uma de cada vez
    private Queue<(string message, Color color)> notificationQueue = new Queue<(string, Color)>();
    private bool isShowingNotification = false;
    
    // Dicionario para registrar o Time.time em que cada mensagem foi exibida pela ultima vez
    private Dictionary<string, float> lastMessageTimes = new Dictionary<string, float>();

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
    //  RESOLUCAO DE NOME DO CAMINHO
    // ════════════════════════════════════════════════════

    private string GetPathName(int pathIndex)
    {
        // EnemyController envia pathIndex + 1, entao precisamos subtrair 1 para acessar o indice correto
        int realIndex = pathIndex - 1;

        if (HordeManager.Instance != null &&
            HordeManager.Instance.spawnPaths != null &&
            realIndex >= 0 &&
            realIndex < HordeManager.Instance.spawnPaths.Count)
        {
            string nome = HordeManager.Instance.spawnPaths[realIndex].pathName;
            if (!string.IsNullOrEmpty(nome))
                return nome;
        }
        return $"Caminho {pathIndex}";
    }

    // ════════════════════════════════════════════════════
    //  GATILHOS DOS OBSERVERS (Rodam no Servidor/Host/Single)
    // ════════════════════════════════════════════════════

    private void TriggerSpawnNotification(int pathIndex)
    {
        string pathName = GetPathName(pathIndex);
        string msg = $"Inimigos nascendo [{pathName}]";
        if (!CanSendMessage(msg)) return;

        if (IsServer) NotifyUIClientRpc(msg, Color.yellow);
        else if (IsLocalFallback()) EnqueueNotification(msg, Color.yellow);
    }

    private void TriggerHalfwayNotification(int pathIndex)
    {
        string pathName = GetPathName(pathIndex);
        string msg = $"Inimigos na metade do caminho [{pathName}]";
        if (!CanSendMessage(msg)) return;

        // Laranja vibrante
        Color orangeColor = new Color(1f, 0.647f, 0f); 
        
        if (IsServer) NotifyUIClientRpc(msg, orangeColor);
        else if (IsLocalFallback()) EnqueueNotification(msg, orangeColor);
    }

    private void TriggerBaseNotification()
    {
        string msg = "Inimigos atingiram a base!";
        if (!CanSendMessage(msg)) return;

        if (IsServer) NotifyUIClientRpc(msg, Color.red);
        else if (IsLocalFallback()) EnqueueNotification(msg, Color.red);
    }

    // Funcao central de Debounce (Cooldown)
    private bool CanSendMessage(string message)
    {
        if (lastMessageTimes.TryGetValue(message, out float lastTime))
        {
            if (Time.time < lastTime + messageCooldown)
            {
                return false; // Spam detectado: bloqueia o envio
            }
        }
        
        // Atualiza ou insere o novo tempo no Dicionario
        lastMessageTimes[message] = Time.time;
        return true;
    }

    private bool IsLocalFallback()
    {
        return HordeManager.Instance != null && HordeManager.Instance.IsLocalMode;
    }

    // ════════════════════════════════════════════════════
    //  REDE E FILA DE NOTIFICACOES
    // ════════════════════════════════════════════════════

    [ClientRpc]
    private void NotifyUIClientRpc(string message, Color messageColor)
    {
        EnqueueNotification(message, messageColor);
    }

    /// <summary>
    /// Adiciona uma notificacao na fila. Se nenhuma estiver sendo exibida, comeca a processar.
    /// </summary>
    private void EnqueueNotification(string message, Color color)
    {
        notificationQueue.Enqueue((message, color));

        if (!isShowingNotification)
        {
            if (queueCoroutine != null) StopCoroutine(queueCoroutine);
            queueCoroutine = StartCoroutine(ProcessNotificationQueue());
        }
    }

    /// <summary>
    /// Processa a fila, exibindo cada notificacao por displayDuration segundos antes de passar para a proxima.
    /// </summary>
    private IEnumerator ProcessNotificationQueue()
    {
        isShowingNotification = true;

        while (notificationQueue.Count > 0)
        {
            var (message, color) = notificationQueue.Dequeue();
            yield return StartCoroutine(FadeRoutine(message, color));
        }

        isShowingNotification = false;
        queueCoroutine = null;
    }

    public void ShowLocalNotification(string message, Color messageColor)
    {
        EnqueueNotification(message, messageColor);
    }

    private IEnumerator FadeRoutine(string message, Color targetColor)
    {
        if (notificationText == null) yield break;

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
