using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// ── HealVFXReactor ───────────────────────────────────────
/// Observa a NetworkVariable<float> de vida do jogador e, a
/// cada vez que o valor SOBE, aciona os efeitos visuais de
/// cura LOCALMENTE em todos os clientes.
///
///  POR QUE NÃO USAMOS RPC AQUI:
///  OnValueChanged da NetworkVariable é propagado pelo NGO
///  automaticamente para todos os clientes quando o servidor
///  altera o valor. Portanto, inscrever o VFX nesse evento
///  é suficiente — sem tráfego de rede extra.
///
///  EFEITOS ACIONADOS:
///  ▸ SupportVFXController.TriggerHealVFX()  — aurora + "+"
///    (re-trigger suave a cada ganho de vida, sem cooldown)
///  ▸ PooledHealAuraVisuals                  — aura completa
///    (re-trigger com cooldown para não spammar instâncias)
///
///  SETUP:
///  Adicione este script no mesmo GameObject que
///  PlayerHealthSystem. Atribua o Prefab HealAura no Inspector.
/// ─────────────────────────────────────────────────────────
/// </summary>
public class HealVFXReactor : NetworkBehaviour
{
    // ── Configurações ─────────────────────────────────────────

    [Header("Prefab da Aura de Cura")]
    [Tooltip("Prefab 'HealAura' contendo o script PooledHealAuraVisuals.")]
    [SerializeField] private GameObject healAuraPrefab;

    [Tooltip("Duração em segundos que o efeito HealAura fica ativo após o spawn.")]
    [SerializeField] private float healAuraDuration = 3.0f;

    [Tooltip("Tempo mínimo (segundos) entre spawns do HealAura Prefab. " +
             "Evita instanciar 60 objetos por segundo durante cura por tick. " +
             "O SupportVFXController (aurora/partículas) NÃO usa este cooldown.")]
    [SerializeField] private float healAuraSpawnCooldown = 2.5f;

    [Tooltip("(Opcional) Ponto de ancoragem para o HealAura. " +
             "Se nulo, usa o Transform raiz deste objeto.")]
    [SerializeField] private Transform vfxAttachPoint;

    // ── Referências ───────────────────────────────────────────

    /// <summary>PlayerHealthSystem no mesmo GameObject.</summary>
    private PlayerHealthSystem _healthSystem;

    /// <summary>
    /// SupportVFXController: controla aurora do cilindro + partículas "+".
    /// Já existe no projeto e lida corretamente com re-trigger.
    /// </summary>
    private SupportVFXController _supportVFXController;

    // ── Estado Interno ────────────────────────────────────────

    /// <summary>Timestamp da última vez que o HealAura Prefab foi spawnado.</summary>
    private float _lastHealAuraSpawnTime = -999f;

    // ── Ciclo de Vida NGO ─────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _healthSystem         = GetComponent<PlayerHealthSystem>();
        _supportVFXController = GetComponent<SupportVFXController>();

        if (vfxAttachPoint == null)
            vfxAttachPoint = transform;

        if (_healthSystem == null)
        {
            Debug.LogError("[HealVFXReactor] PlayerHealthSystem não encontrado! " +
                           "Certifique-se de que ambos os scripts estão no mesmo GameObject.");
            return;
        }

        // ── INSCRIÇÃO NO EVENTO ───────────────────────────────
        // OnValueChanged dispara em TODOS os clientes (e no servidor)
        // sempre que o servidor altera currentHealth.Value.
        // Não precisamos de RPCs adicionais para propagar o visual.
        _healthSystem.currentHealth.OnValueChanged += OnHealthValueChanged;

        Debug.Log($"[HealVFXReactor] Inscrito em currentHealth.OnValueChanged para {gameObject.name}.");
    }

    public override void OnNetworkDespawn()
    {
        // Desinscreve para evitar memory leaks e callbacks órfãos
        if (_healthSystem != null)
            _healthSystem.currentHealth.OnValueChanged -= OnHealthValueChanged;

        _healthSystem         = null;
        _supportVFXController = null;

        base.OnNetworkDespawn();
    }

    // ── Listener do OnValueChanged ────────────────────────────

    /// <summary>
    /// Chamado automaticamente em TODOS os clientes (e servidor)
    /// sempre que o servidor altera currentHealth.Value.
    /// </summary>
    /// <param name="previousValue">Valor de vida antes da alteração.</param>
    /// <param name="newValue">Novo valor de vida após a alteração.</param>
    private void OnHealthValueChanged(float previousValue, float newValue)
    {
        // Apenas ganho de vida aciona o VFX; dano e sem alteração são ignorados.
        if (newValue <= previousValue)
            return;

        // ── EFEITO 1: SupportVFXController ───────────────────
        // Aciona a cada tick de cura sem cooldown.
        // O próprio SupportVFXController gerencia re-trigger suavemente
        // (cancela a corrotina anterior e recomeça), então é seguro chamar
        // repetidamente mesmo durante cura contínua por frame.
        if (_supportVFXController != null)
        {
            _supportVFXController.TriggerHealVFX();
        }

        // ── EFEITO 2: HealAura Prefab ─────────────────────────
        // Controlado por cooldown para evitar spam de instâncias
        // durante cura passiva (que ocorre a cada frame via Update).
        if (Time.time >= _lastHealAuraSpawnTime + healAuraSpawnCooldown)
        {
            _lastHealAuraSpawnTime = Time.time;
            SpawnHealAuraVFX();
        }
    }

    // ── Spawn do HealAura ─────────────────────────────────────

    /// <summary>
    /// Instancia o Prefab HealAura e o ativa via PooledHealAuraVisuals.
    /// Compatível com Pool Managers externos: substitua o Instantiate/Destroy
    /// pelo Get/Return do seu manager quando ele estiver pronto.
    /// </summary>
    private void SpawnHealAuraVFX()
    {
        if (healAuraPrefab == null)
        {
            Debug.LogWarning("[HealVFXReactor] healAuraPrefab não atribuído no Inspector!");
            return;
        }

        // Usa a nova infraestrutura global para não gerar Garbage
        GameObject auraInstance = GlobalVFXPool.GetVFX(healAuraPrefab, vfxAttachPoint.position, vfxAttachPoint.rotation);

        PooledHealAuraVisuals auraVisuals = auraInstance.GetComponent<PooledHealAuraVisuals>();

        if (auraVisuals == null)
        {
            Debug.LogError("[HealVFXReactor] O Prefab HealAura não possui PooledHealAuraVisuals!");
            GlobalVFXPool.ReleaseVFX(healAuraPrefab, auraInstance);
            return;
        }

        // Callback invocado quando a duração do efeito termina
        auraVisuals.OnReturnToPool = (go) => GlobalVFXPool.ReleaseVFX(healAuraPrefab, go);

        // Ativa parenteando ao attach point deste jogador
        auraVisuals.OnSpawnFromPool(vfxAttachPoint, healAuraDuration);
    }
}
