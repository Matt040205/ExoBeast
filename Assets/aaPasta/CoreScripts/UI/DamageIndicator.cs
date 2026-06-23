using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

[RequireComponent(typeof(Image))]
public class DamageIndicator : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("A Imagem da UI que representa o indicador de dano (vignette/sangue).")]
    public Image damageImage;

    [Tooltip("Transform que servira de base para calcular a direcao (geralmente a Camera Principal ou o Transform do Jogador local).")]
    public Transform referenceTransform;

    [Tooltip("Referencia opcional ao PlayerHealthSystem do jogador local. Se vazio, o script tentara encontrar automaticamente na cena quando o jogador nascer.")]
    public PlayerHealthSystem localPlayerHealth;

    [Header("Configuracoes de Animacao")]
    [Tooltip("Tempo em segundos que leva para a imagem desaparecer completamente.")]
    public float fadeDuration = 1.5f;

    [Tooltip("Cor base do indicador (pode ajustar o alpha maximo aqui).")]
    public Color indicatorColor = new Color(1f, 0f, 0f, 1f);

    private RectTransform rectTransform;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (damageImage == null)
            damageImage = GetComponent<Image>();

        rectTransform = damageImage.rectTransform;
        
        // Inicia invisivel
        Color startColor = indicatorColor;
        startColor.a = 0f;
        damageImage.color = startColor;
    }

    private void Start()
    {
        // Se a referencia nao foi setada no inspector, tenta encontrar a camera principal
        if (referenceTransform == null && Camera.main != null)
        {
            referenceTransform = Camera.main.transform;
        }

        // Tenta encontrar o player local caso nao tenha sido atribuido
        StartCoroutine(FindLocalPlayerRoutine());
    }

    private IEnumerator FindLocalPlayerRoutine()
    {
        while (localPlayerHealth == null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                var localPlayerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
                if (localPlayerObject != null)
                {
                    localPlayerHealth = localPlayerObject.GetComponent<PlayerHealthSystem>();
                    if (localPlayerHealth != null)
                    {
                        RegisterToHealthEvents();
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnEnable()
    {
        if (localPlayerHealth != null)
        {
            RegisterToHealthEvents();
        }
    }

    private void OnDisable()
    {
        if (localPlayerHealth != null)
        {
            localPlayerHealth.OnLocalDamageTaken -= HandleDamageTaken;
        }
    }

    private void RegisterToHealthEvents()
    {
        // Garante que nao vamos assinar duas vezes
        localPlayerHealth.OnLocalDamageTaken -= HandleDamageTaken;
        localPlayerHealth.OnLocalDamageTaken += HandleDamageTaken;
    }

    /// <summary>
    /// Metodo chamado pelo PlayerHealthSystem quando o jogador local recebe dano.
    /// </summary>
    public void HandleDamageTaken(float damage, Vector3 attackerPosition, bool hasAttacker)
    {
        if (!hasAttacker || referenceTransform == null)
        {
            // Dano sem direcao (ex: veneno) ou sem referencia - apenas mostra o vignette centralizado
            ShowIndicator(0f);
            return;
        }

        Vector3 playerPos = localPlayerHealth.transform.position;

        // Ignora diferencas de altura (eixo Y) para o calculo direcional
        Vector3 directionToAttacker = attackerPosition - playerPos;
        directionToAttacker.y = 0f;
        directionToAttacker.Normalize();

        Vector3 referenceForward = referenceTransform.forward;
        referenceForward.y = 0f;
        referenceForward.Normalize();

        // Se a direcao for zero (ex: atacante exatamente na mesma posicao), evita erros
        if (directionToAttacker == Vector3.zero || referenceForward == Vector3.zero)
        {
            ShowIndicator(0f);
            return;
        }

        // Calcula o angulo entre a frente da referencia (camera/jogador) e a direcao do atacante
        float angle = Vector3.SignedAngle(referenceForward, directionToAttacker, Vector3.up);

        ShowIndicator(angle);
    }

    private void ShowIndicator(float angle)
    {
        // Aplica a rotacao no eixo Z da UI. 
        // No Unity, rotacionar negativamente no eixo Z gira no sentido horario.
        // Assumindo que a imagem base (angle = 0) representa dano vindo de frente (em cima na tela).
        rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);

        // Reinicia a animacao de fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        // Define o Alpha maximo inicial
        damageImage.color = indicatorColor;
        
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Interpola o alpha de 1 (ou indicatorColor.a) ate 0
            float newAlpha = Mathf.Lerp(indicatorColor.a, 0f, elapsedTime / fadeDuration);
            
            Color newColor = damageImage.color;
            newColor.a = newAlpha;
            damageImage.color = newColor;

            yield return null;
        }

        // Garante que o Alpha termine em 0
        Color finalColor = damageImage.color;
        finalColor.a = 0f;
        damageImage.color = finalColor;
        
        fadeCoroutine = null;
    }
}
