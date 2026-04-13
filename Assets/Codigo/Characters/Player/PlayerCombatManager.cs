using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PlayerCombatManager ────────────────────────────────
/// Gerencia troca entre modos de combate (Ranged / Melee).
///
///  ▸ NetworkVariable netCombatType: sincroniza tipo de combate para todos
///  ▸ Owner: Tab para trocar arma → RequestSwitchWeaponServerRpc
///  ▸ Todos: OnCombatTypeChanged atualiza visuais (modelos 3D de armas)
///  ▸ Owner: ativa/desativa PlayerShooting e MeleeCombatSystem conforme modo
/// ─────────────────────────────────────────────────────
/// </summary>
public class PlayerCombatManager : NetworkBehaviour
{
    [Header("Dados e Lógica")]
    public CharacterBase characterData;
    public PlayerShooting shootingSystem;
    public MeleeCombatSystem meleeSystem;
    public PlayerHealthSystem healthSystem;

    [Header("Visuals (Modelos 3D)")]
    public GameObject meleeWeaponModel;
    public GameObject rangedWeaponModel;

    // NetworkVariable para manter sincronizado o tipo de combate atual
    public NetworkVariable<CombatType> netCombatType = new NetworkVariable<CombatType>(
        CombatType.Ranged,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        netCombatType.OnValueChanged += OnCombatTypeChanged;
        UpdateCombatStateVisuals(netCombatType.Value);

        if (IsOwner)
        {
            if (healthSystem != null) healthSystem.characterData = characterData;
            if (shootingSystem != null) shootingSystem.characterData = characterData;
            if (meleeSystem != null) meleeSystem.characterData = characterData;
            
            UpdateAttackScripts(netCombatType.Value);
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            CombatType nextType = (netCombatType.Value == CombatType.Ranged) ? CombatType.Melee : CombatType.Ranged;
            RequestSwitchWeaponServerRpc(nextType);
        }
    }

    [ServerRpc]
    private void RequestSwitchWeaponServerRpc(CombatType newType)
    {
        netCombatType.Value = newType;
    }

    private void OnCombatTypeChanged(CombatType oldType, CombatType newType)
    {
        UpdateCombatStateVisuals(newType);
        
        if (IsOwner)
        {
            UpdateAttackScripts(newType);
        }
    }

    private void UpdateCombatStateVisuals(CombatType type)
    {
        if (type == CombatType.Ranged)
        {
            if (meleeWeaponModel != null) meleeWeaponModel.SetActive(false);
            if (rangedWeaponModel != null) rangedWeaponModel.SetActive(true);
        }
        else // Melee
        {
            if (meleeWeaponModel != null) meleeWeaponModel.SetActive(true);
            if (rangedWeaponModel != null) rangedWeaponModel.SetActive(false);
        }
    }

    private void UpdateAttackScripts(CombatType type)
    {
        if (shootingSystem != null) shootingSystem.enabled = (type == CombatType.Ranged);
        if (meleeSystem != null) meleeSystem.enabled = (type == CombatType.Melee);
    }

    public override void OnNetworkDespawn()
    {
        netCombatType.OnValueChanged -= OnCombatTypeChanged;
        base.OnNetworkDespawn();
    }
}
