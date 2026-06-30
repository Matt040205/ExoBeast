using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class SpiderWebDebuffPlayer : NetworkBehaviour
{
    [Header("UI - Mensagens")]
    [Tooltip("Texto exibido quando o jogador está preso. Use {0} para mostrar a quantidade de cliques restantes.")]
    public string textoPreso = "PRESO! Aperte ESPAÇO {0} vezes para se libertar!";

    private PlayerHealthSystem healthSystem;
    private PlayerMovement movement;

    private int hitCount = 0;
    private float slowTimer = 0f;
    private const float SLOW_DURATION = 3f;
    
    private int spacePressesRemaining = 4;
    private bool isTrapped = false;

    private Coroutine debuffCoroutine;

    private void Awake()
    {
        healthSystem = GetComponent<PlayerHealthSystem>();
        movement = GetComponent<PlayerMovement>();
    }

    public void OnHit(bool enableTrap)
    {
        if (!IsServer) return;
        if (healthSystem == null || movement == null) return;

        var ultimate = GetComponent<NineTailsDanceLogic>();
        if (ultimate != null && ultimate.IsUltimateActive) return;

        if (isTrapped) return;

        hitCount++;
        slowTimer = SLOW_DURATION;

        // Aplica lentidão (50% de velocidade) no servidor
        healthSystem.speedMultiplier.Value = 0.5f;

        // Verifica se ultrapassou o limite de teias para prender
        if (hitCount >= 5 && enableTrap)
        {
            TrapPlayer();
        }
        else
        {
            // Renova ou inicia a corrotina de debuff
            if (debuffCoroutine != null) StopCoroutine(debuffCoroutine);
            debuffCoroutine = StartCoroutine(SlowCountdown());
        }
    }

    private void TrapPlayer()
    {
        isTrapped = true;
        spacePressesRemaining = 4;

        movement.netIsWebTrapped.Value = true;
        healthSystem.speedMultiplier.Value = 0.5f;

        if (debuffCoroutine != null) StopCoroutine(debuffCoroutine);

        // Envia RPC para mostrar o alerta no cliente dono
        ShowTrappedNotificationClientRpc(spacePressesRemaining);
    }

    public void RegisterSpacePress()
    {
        if (!IsServer || !isTrapped) return;

        spacePressesRemaining--;
        Debug.Log($"[SpiderWebDebuffPlayer] Cliques restantes para se libertar: {spacePressesRemaining}");

        if (spacePressesRemaining <= 0)
        {
            ReleasePlayer();
        }
        else
        {
            // Atualiza a notificação na tela do jogador
            ShowTrappedNotificationClientRpc(spacePressesRemaining);
        }
    }

    private void ReleasePlayer()
    {
        isTrapped = false;
        hitCount = 0;

        movement.netIsWebTrapped.Value = false;
        healthSystem.speedMultiplier.Value = 1f; // restaura velocidade total

        // Limpa a notificação na tela do jogador
        ClearTrappedNotificationClientRpc();
    }

    private IEnumerator SlowCountdown()
    {
        while (slowTimer > 0)
        {
            slowTimer -= Time.deltaTime;
            yield return null;
        }

        if (!isTrapped)
        {
            hitCount = 0;
            healthSystem.speedMultiplier.Value = 1f;
        }
    }

    [ClientRpc]
    private void ShowTrappedNotificationClientRpc(int presses)
    {
        if (!IsOwner) return;

        if (PlayerHUD.Instance != null)
        {
            string formattedMsg = string.Format(textoPreso, presses);
            PlayerHUD.Instance.SetAlertaPresoText(formattedMsg);
        }
    }

    [ClientRpc]
    private void ClearTrappedNotificationClientRpc()
    {
        if (!IsOwner) return;

        if (PlayerHUD.Instance != null)
        {
            PlayerHUD.Instance.SetAlertaPresoText("");
        }
    }

    public override void OnDestroy()
    {
        if (IsServer)
        {
            if (movement != null) movement.netIsWebTrapped.Value = false;
            if (healthSystem != null) healthSystem.speedMultiplier.Value = 1f;
        }

        base.OnDestroy();
    }
}
