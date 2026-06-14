using UnityEngine;
using System.Collections;
using Unity.Netcode;

/// <summary>
/// ── NineTailsDanceLogic ──────────────────────────────────
/// Activates the Nine Tails Dance ultimate: forces Melee combat mode with boosted stats.
///
///  ▸ netIsUltimateActive syncs visual state to all clients
///  ▸ SetUltimateStateServerRpc mutates netCombatType on PlayerCombatManager
///  ▸ OnNetworkDespawn unsubscribes the callback to prevent duplicate invocations on respawn
/// ─────────────────────────────────────────────────────────
/// </summary>
public class NineTailsDanceLogic : NetworkBehaviour
{
    [Header("Configuracoes da Ultimate")]
    public float ultimateAttackSpeed = 5f;
    public float ultimateAttackRange = 3f;
    public float ultimateAttackAngle = 360f;

    private PlayerCombatManager combatManager;
    private PlayerShooting shootingSystem;
    private MeleeCombatSystem meleeSystem;
    private Animator anim;

    private float originalAttackRange;
    private CombatType previousCombatType;

    private NetworkVariable<bool> netIsUltimateActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsUltimateActive => netIsUltimateActive.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        combatManager = GetComponent<PlayerCombatManager>();
        shootingSystem = GetComponent<PlayerShooting>();
        meleeSystem = GetComponent<MeleeCombatSystem>();
        anim = GetComponentInChildren<Animator>();

        netIsUltimateActive.OnValueChanged += OnUltimateStateChanged;

        if (netIsUltimateActive.Value)
        {
            ApplyUltimateEffects();
        }
    }

    public void StartEffect(float duration)
    {
        // StartEffect é chamado pelo servidor (via Activate() no RequestActivateAbilityServerRpc)
        if (!IsServer) return;

        netIsUltimateActive.Value = true;
        if (combatManager != null)
        {
            previousCombatType = combatManager.netCombatType.Value;
            combatManager.netCombatType.Value = CombatType.Melee;
        }

        StartCoroutine(UltimateTimerCoroutine(duration));
    }

    private IEnumerator UltimateTimerCoroutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            if (IsServer && netIsUltimateActive.Value)
            {
                var movement = GetComponent<PlayerMovement>();
                if (movement != null && movement.enabled)
                {
                    Collider[] hits = Physics.OverlapSphere(transform.position, 5.0f);
                    foreach (var hit in hits)
                    {
                        EnemyHealthSystem enemyHealth = hit.GetComponent<EnemyHealthSystem>();
                        if (enemyHealth != null)
                        {
                            enemyHealth.TakeDamage(2f, 0f, false, OwnerClientId);
                        }
                    }
                }
            }
        }

        if (IsServer)
        {
            netIsUltimateActive.Value = false;
            if (combatManager != null)
                combatManager.netCombatType.Value = previousCombatType;
        }
    }

    [ServerRpc]
    private void SetUltimateStateServerRpc(bool active)
    {
        netIsUltimateActive.Value = active;

        if (combatManager != null)
        {
            if (active)
            {
                previousCombatType = combatManager.netCombatType.Value;
                combatManager.netCombatType.Value = CombatType.Melee;
            }
            else
            {
                combatManager.netCombatType.Value = previousCombatType;
            }
        }
    }

    private void OnUltimateStateChanged(bool wasActive, bool isActive)
    {
        if (isActive) ApplyUltimateEffects();
        else RemoveUltimateEffects();
    }

    private void ApplyUltimateEffects()
    {
        if (anim != null) anim.SetBool("KatanaArmed", true);

        if (meleeSystem != null && meleeSystem.swordStats != null)
        {
            originalAttackRange = meleeSystem.swordStats.attackRange;
            meleeSystem.swordStats.attackRange = ultimateAttackRange;
            meleeSystem.overrideAttackAngle = ultimateAttackAngle;
            meleeSystem.overrideAttackSpeed = ultimateAttackSpeed;
        }

        if (shootingSystem != null) shootingSystem.enabled = false;
        if (meleeSystem != null) meleeSystem.enabled = true;
    }

    private void RemoveUltimateEffects()
    {
        if (anim != null) anim.SetBool("KatanaArmed", false);

        if (meleeSystem != null && meleeSystem.swordStats != null)
        {
            meleeSystem.swordStats.attackRange = originalAttackRange;
            meleeSystem.overrideAttackAngle = null;
            meleeSystem.overrideAttackSpeed = null;
        }

        // Disable instead of destroy — this component lives on the player's NetworkObject
        this.enabled = false;
    }

    public override void OnNetworkDespawn()
    {
        netIsUltimateActive.OnValueChanged -= OnUltimateStateChanged;
        base.OnNetworkDespawn();
    }
}
