using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Unity.Netcode;

/// <summary>
/// ── HealingSkillController ────────────────────────────────
/// Gerencia a habilidade de cura ativa do jogador em multiplayer
/// usando Unity Netcode for GameObjects (NGO).
///
///  FLUXO DE REDE:
///  ▸ Owner pressiona a tecla de cura → RequestHealServerRpc()
///  ▸ Servidor valida (cooldown, HP, mana) e aplica a cura via PlayerHealthSystem
///  ▸ Servidor invoca TriggerHealVFXClientRpc() para TODOS os clientes
///  ▸ Cada cliente instancia/spawna o Prefab HealAura no transform do jogador
///    e reproduce os efeitos visuais localmente (sem custo de rede extra)
///
///  INTEGRAÇÃO:
///  ▸ Adicione este script ao mesmo GameObject que PlayerHealthSystem.
///  ▸ Atribua o Prefab "HealAura" (com PooledHealAuraVisuals) no Inspector.
///  ▸ Opcionalmente atribua um Pool Manager externo; se nulo, o script
///    instancia/destrói diretamente (fallback seguro para prototipagem).
/// ─────────────────────────────────────────────────────────
/// </summary>
public class HealingSkillController : NetworkBehaviour
{
    // ── Configurações da Habilidade ───────────────────────────

    [Header("Configurações da Habilidade")]
    [Tooltip("Tecla que o dono do objeto usa para ativar a cura.")]
    [SerializeField] private KeyCode healKey = KeyCode.H;

    [Tooltip("Quantidade de HP restaurada por ativação.")]
    [SerializeField] private float healAmount = 30f;

    [Tooltip("Tempo em segundos entre usos da habilidade.")]
    [SerializeField] private float skillCooldown = 8f;

    // ── Configurações do Prefab de VFX ────────────────────────

    [Header("Prefab de VFX (HealAura)")]
    [Tooltip("Prefab do HealAura. Deve conter o script PooledHealAuraVisuals.")]
    [SerializeField] private GameObject healAuraPrefab;

    [Tooltip("Duração em segundos que o efeito visual do HealAura permanece ativo.")]
    [SerializeField] private float healVFXDuration = 3.0f;

    [Tooltip("(Opcional) Transform onde o HealAura será centralizado. " +
             "Se nulo, usa o Transform raiz deste NetworkObject.")]
    [SerializeField] private Transform vfxAttachPoint;

    // ── Referências Internas ──────────────────────────────────

    /// <summary>
    /// Referência ao PlayerHealthSystem no mesmo GameObject.
    /// Usada pelo servidor para aplicar a cura de forma autoritativa.
    /// </summary>
    private PlayerHealthSystem _healthSystem;

    /// <summary>
    /// Referência ao SupportVFXController no mesmo GameObject.
    /// Permite também acionar o VFX simples legado de cura (aurora/partículas +),
    /// mantendo compatibilidade com o que já existe no projeto.
    /// </summary>
    private SupportVFXController _supportVFXController;

    // ── Controle de Cooldown (Servidor) ──────────────────────

    private float _lastSkillUseTime = -999f;

    // ── Ciclo de Vida NGO ─────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _healthSystem = GetComponent<PlayerHealthSystem>();
        _supportVFXController = GetComponent<SupportVFXController>();

        if (_healthSystem == null)
        {
            Debug.LogError("[HealingSkillController] PlayerHealthSystem não encontrado no mesmo GameObject! " +
                           "Certifique-se de que ambos os scripts estão no mesmo prefab.");
        }

        // O VFX attach point padrão é o próprio transform, se não atribuído
        if (vfxAttachPoint == null)
        {
            vfxAttachPoint = transform;
        }
    }

    // ── Input (apenas o dono local) ───────────────────────────

    void Update()
    {
        // Apenas o dono deste NetworkObject detecta o input da habilidade
        if (!IsOwner) return;

        if (Input.GetKeyDown(healKey))
        {
            RequestHealServerRpc();
        }
    }

    // ── ServerRpc: Requisição de Cura ─────────────────────────

    /// <summary>
    /// Chamado pelo cliente dono para solicitar a cura ao servidor.
    /// O servidor é a única autoridade que valida e aplica a habilidade.
    /// </summary>
    [ServerRpc]
    private void RequestHealServerRpc()
    {
        // --- Validação no Servidor ---

        // 1. Verifica cooldown da habilidade
        if (Time.time < _lastSkillUseTime + skillCooldown)
        {
            float remaining = (_lastSkillUseTime + skillCooldown) - Time.time;
            Debug.Log($"[HealingSkillController] Habilidade em cooldown. Aguarde {remaining:F1}s.");
            return;
        }

        // 2. Verifica se o PlayerHealthSystem está disponível
        if (_healthSystem == null)
        {
            Debug.LogWarning("[HealingSkillController] PlayerHealthSystem ausente. Cura cancelada.");
            return;
        }

        // 3. Verifica se o jogador já está com HP máximo
        if (_healthSystem.characterData != null &&
            _healthSystem.currentHealth.Value >= _healthSystem.characterData.maxHealth)
        {
            Debug.Log("[HealingSkillController] Jogador já está com HP máximo. Cura ignorada.");
            return;
        }

        // --- Aplicação Autoritativa no Servidor ---

        // Aplica a cura via método existente em PlayerHealthSystem
        _healthSystem.Heal(healAmount);
        _lastSkillUseTime = Time.time;

        Debug.Log($"[HealingSkillController] Servidor aplicou {healAmount} de cura ao jogador {OwnerClientId}.");

        // Notifica TODOS os clientes para renderizarem o efeito visual
        TriggerHealVFXClientRpc();
    }

    // ── ClientRpc: Acionamento do VFX ─────────────────────────

    /// <summary>
    /// Chamado pelo servidor em TODOS os clientes (incluindo o servidor-host).
    /// Cada cliente renderiza o efeito visual do HealAura de forma independente,
    /// sem tráfego de rede adicional além desta chamada.
    /// </summary>
    [ClientRpc]
    private void TriggerHealVFXClientRpc()
    {
        // 1. Acionar o SupportVFXController legado (aurora do cilindro + partículas "+")
        //    se disponível no mesmo GameObject — mantém compatibilidade com o que já existe.
        if (_supportVFXController != null)
        {
            _supportVFXController.TriggerHealVFX();
        }

        // 2. Spawn do Prefab HealAura (pool ou instanciação direta)
        SpawnHealAuraVFX();
    }

    // ── Spawn do HealAura VFX ─────────────────────────────────

    /// <summary>
    /// Instancia ou obtém do pool o Prefab HealAura e o ativa via PooledHealAuraVisuals.
    /// Se nenhum pool manager estiver configurado, faz instanciação direta como fallback.
    /// </summary>
    private void SpawnHealAuraVFX()
    {
        if (healAuraPrefab == null)
        {
            Debug.LogWarning("[HealingSkillController] healAuraPrefab não atribuído no Inspector!");
            return;
        }

        // Tenta obter do pool (se o PoolingManager do projeto seguir o padrão de callback)
        // NOTA: Integre aqui a chamada ao seu PoolingManager assim que ele estiver pronto.
        // Exemplo:  GameObject instance = MyPoolManager.Instance.Get(healAuraPrefab);
        //
        // Por ora, instanciação direta (fallback seguro para desenvolvimento):
        GameObject auraInstance = Instantiate(healAuraPrefab);

        PooledHealAuraVisuals auraVisuals = auraInstance.GetComponent<PooledHealAuraVisuals>();

        if (auraVisuals == null)
        {
            Debug.LogError("[HealingSkillController] O Prefab HealAura não possui o componente " +
                           "PooledHealAuraVisuals! Verifique o Prefab.");
            Destroy(auraInstance);
            return;
        }

        // Registra o callback de retorno ao pool.
        // Quando a duração expirar, PooledHealAuraVisuals invocará este delegate.
        auraVisuals.OnReturnToPool = (go) =>
        {
            // NOTA: Substitua Destroy() pela devolução ao pool quando o manager estiver integrado.
            // Exemplo:  MyPoolManager.Instance.Return(go);
            Destroy(go);
        };

        // Ativa o efeito visual parenteado ao attach point deste jogador
        auraVisuals.OnSpawnFromPool(vfxAttachPoint, healVFXDuration);
    }

    // ── Limpeza ───────────────────────────────────────────────

    public override void OnNetworkDespawn()
    {
        // Limpa referências ao ser despawnado da rede
        _healthSystem = null;
        _supportVFXController = null;
        base.OnNetworkDespawn();
    }
}
