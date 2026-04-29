using System.Collections.Generic;
using FMODUnity;
using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Sync;

/// <summary>
/// â”€â”€ CommanderAbilityController â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
/// Controlador de habilidades do personagem (Q, E, X).
///
///  â–¸ Owner: detecta input e envia RequestActivateAbilityServerRpc
///  â–¸ Server: valida cooldown, executa Ability.Activate(), notifica clientes
///  â–¸ NetworkVariable netUltimateCharge: carga da ultimate (dano + tempo)
///  â–¸ Suporta passiva via characterData.passive.OnEquip()
/// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
/// </summary>
public class CommanderAbilityController : NetworkBehaviour
{
    public CharacterBase characterData;
    private Animator anim;
    private PlayerHealthSystem playerHealth;
    private LocalPlayerInputBridge inputBridge;
    private bool abilitiesInitialized;
    private bool passiveEquipped;

    public Dictionary<Ability, float> abilityCooldowns = new Dictionary<Ability, float>();

    public float ultimateChargeThreshold = 100f;

    public NetworkVariable<float> netUltimateCharge = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public float CurrentUltimateCharge
    {
        get
        {
            if (ultimateChargeThreshold <= 0) return 0;
            return Mathf.Clamp01(netUltimateCharge.Value / ultimateChargeThreshold);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerHealth = GetComponent<PlayerHealthSystem>();
        anim = GetComponentInChildren<Animator>();
        inputBridge = GetComponent<LocalPlayerInputBridge>();

        if (IsOwner && playerHealth != null)
            playerHealth.OnDamageDealt += HandleDamageDealt;

        if (!EnsureCharacterDataInitialized())
        {
            Debug.LogWarning(
                $"[CommanderAbilityController] characterData ainda nulo apÃ³s resoluÃ§Ã£o local. " +
                $"Habilidades desabilitadas para clientId={OwnerClientId}.");
        }
    }

    private T ResolveAbilityOfType<T>() where T : Ability
    {
        if (!EnsureCharacterDataInitialized())
            return null;

        if (characterData.ability1 is T ability1)
            return ability1;

        if (characterData.ability2 is T ability2)
            return ability2;

        if (characterData.ultimate is T ultimate)
            return ultimate;

        return null;
    }

    private bool CanSendOwnerOnlyAbilityProxy()
    {
        return IsServer &&
               NetworkManager.Singleton != null &&
               OwnerClientId != NetworkManager.ServerClientId;
    }

    private ClientRpcParams BuildOwnerOnlyRpcParams()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
    }

    void OnDestroy()
    {
        if (IsOwner && playerHealth != null)
            playerHealth.OnDamageDealt -= HandleDamageDealt;

        if (passiveEquipped && characterData != null && characterData.passive != null)
        {
            characterData.passive.OnUnequip(gameObject);
            passiveEquipped = false;
        }
    }

    void Update()
    {
        EnsureCharacterDataInitialized();

        if (IsServer &&
            netUltimateCharge.Value < ultimateChargeThreshold &&
            characterData != null &&
            characterData.ultimateChargePerSecond > 0)
        {
            netUltimateCharge.Value = Mathf.Min(
                netUltimateCharge.Value + characterData.ultimateChargePerSecond * Time.deltaTime,
                ultimateChargeThreshold);
        }

        if (IsServer || IsOwner)
        {
            List<Ability> keys = new List<Ability>(abilityCooldowns.Keys);
            foreach (Ability ability in keys)
            {
                if (abilityCooldowns[ability] > 0)
                {
                    abilityCooldowns[ability] -= Time.deltaTime;
                    if (abilityCooldowns[ability] < 0)
                        abilityCooldowns[ability] = 0;
                }
            }
        }

        if (!IsOwner)
            return;

        if (GetComponent<MergulhoTintaLogic>() != null)
        {
            if (inputBridge == null)
                inputBridge = GetComponent<LocalPlayerInputBridge>();

            inputBridge?.ConsumeAbility1Pressed();
            inputBridge?.ConsumeAbility2Pressed();
            inputBridge?.ConsumeUltimatePressed();
            return;
        }

        if (inputBridge == null)
            inputBridge = GetComponent<LocalPlayerInputBridge>();

        if (inputBridge == null || !inputBridge.isActiveAndEnabled)
            return;

        if (inputBridge.ConsumeAbility1Pressed()) RequestActivateAbilityServerRpc(0);
        if (inputBridge.ConsumeAbility2Pressed()) RequestActivateAbilityServerRpc(1);
        if (inputBridge.ConsumeUltimatePressed()) RequestActivateUltimateServerRpc();
    }

    [ServerRpc]
    private void RequestActivateAbilityServerRpc(int abilityIndex)
    {
        if (!EnsureCharacterDataInitialized())
            return;

        Ability abilityToUse = null;
        if (abilityIndex == 0) abilityToUse = characterData.ability1;
        else if (abilityIndex == 1) abilityToUse = characterData.ability2;

        if (abilityToUse == null)
            return;

        if (abilityCooldowns.ContainsKey(abilityToUse) && abilityCooldowns[abilityToUse] > 0)
            return;

        bool started = abilityToUse.Activate(gameObject);

        if (started)
        {
            abilityCooldowns[abilityToUse] = abilityToUse.cooldown;
            ActivateAbilityVisualClientRpc(abilityIndex);
        }
    }

    [ClientRpc]
    private void ActivateAbilityVisualClientRpc(int abilityIndex)
    {
        if (!EnsureCharacterDataInitialized())
            return;

        Ability ability = null;
        if (abilityIndex == 0) ability = characterData.ability1;
        else if (abilityIndex == 1) ability = characterData.ability2;

        if (ability == null)
            return;

        abilityCooldowns[ability] = ability.cooldown;
    }

    public void StartLocalMergulhoTintaOwnerProxy(
        float duration,
        float exitDamage,
        float damageRadius)
    {
        HabilidadeMergulhoTinta ability = ResolveAbilityOfType<HabilidadeMergulhoTinta>();
        if (ability == null || !CanSendOwnerOnlyAbilityProxy())
            return;

        StartLocalMergulhoTintaOwnerClientRpc(
            duration,
            exitDamage,
            damageRadius,
            BuildOwnerOnlyRpcParams());
    }

    [ClientRpc]
    private void StartLocalMergulhoTintaOwnerClientRpc(
        float duration,
        float exitDamage,
        float damageRadius,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
            return;

        HabilidadeMergulhoTinta ability = ResolveAbilityOfType<HabilidadeMergulhoTinta>();
        if (ability == null || GetComponent<MergulhoTintaLogic>() != null)
            return;

        MergulhoTintaLogic logic = gameObject.AddComponent<MergulhoTintaLogic>();
        logic.StartDive(
            duration,
            exitDamage,
            damageRadius,
            ability.visualPuddlePrefab,
            ability,
            false);
    }

    public void CompleteLocalMergulhoTintaOwnerProxy(Vector3 surfacePosition)
    {
        if (!CanSendOwnerOnlyAbilityProxy())
            return;

        CompleteLocalMergulhoTintaOwnerClientRpc(surfacePosition, BuildOwnerOnlyRpcParams());
    }

    [ClientRpc]
    private void CompleteLocalMergulhoTintaOwnerClientRpc(
        Vector3 surfacePosition,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
            return;

        MergulhoTintaLogic logic = GetComponent<MergulhoTintaLogic>();
        if (logic != null)
            logic.CompleteOwnerProxySurfaceExit(surfacePosition);
    }

    public void StartLocalObraPrimaOwnerProxy(
        float duration,
        int shotsCount,
        float damagePerShot,
        float radius,
        float silenceDuration)
    {
        HabilidadeObraPrima ability = ResolveAbilityOfType<HabilidadeObraPrima>();
        if (ability == null || ability.logicPrefab == null || !CanSendOwnerOnlyAbilityProxy())
            return;

        StartLocalObraPrimaOwnerClientRpc(
            duration,
            shotsCount,
            damagePerShot,
            radius,
            silenceDuration,
            BuildOwnerOnlyRpcParams());
    }

    [ClientRpc]
    private void StartLocalObraPrimaOwnerClientRpc(
        float duration,
        int shotsCount,
        float damagePerShot,
        float radius,
        float silenceDuration,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
            return;

        HabilidadeObraPrima ability = ResolveAbilityOfType<HabilidadeObraPrima>();
        if (ability == null || ability.logicPrefab == null)
            return;

        ObraPrimaLogic logic = Instantiate(ability.logicPrefab, transform);
        logic.StartUltimate(gameObject, duration, shotsCount, damagePerShot, radius, silenceDuration, false);
    }

    public void StartLocalAquiNaoOwnerProxy(Vector3 pos, Quaternion rot, string sfxSwing)
    {
        HabilidadeAquiNao ability = ResolveAbilityOfType<HabilidadeAquiNao>();
        if (ability == null || !CanSendOwnerOnlyAbilityProxy())
            return;

        StartLocalAquiNaoOwnerClientRpc(pos, rot, sfxSwing, BuildOwnerOnlyRpcParams());
    }

    [ClientRpc]
    private void StartLocalAquiNaoOwnerClientRpc(
        Vector3 pos,
        Quaternion rot,
        string sfxSwing,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
            return;

        HabilidadeAquiNao ability = ResolveAbilityOfType<HabilidadeAquiNao>();
        if (ability == null)
            return;

        if (!string.IsNullOrEmpty(sfxSwing))
            RuntimeManager.PlayOneShot(sfxSwing, pos);

        if (ability.logicPrefab != null)
        {
            AquiNaoLogic logic = Object.Instantiate(ability.logicPrefab, pos, rot);
            Object.Destroy(logic.gameObject, 0.5f);
        }
    }

    public void StartLocalBombaSprayOwnerProxy(
        Vector3 spawnPos,
        Vector3 direction,
        float force,
        float radius,
        float duration)
    {
        HabilidadeBombaSpray ability = ResolveAbilityOfType<HabilidadeBombaSpray>();
        if (ability?.projectilePrefab == null || !CanSendOwnerOnlyAbilityProxy())
            return;

        StartLocalBombaSprayOwnerClientRpc(
            spawnPos,
            direction,
            force,
            radius,
            duration,
            BuildOwnerOnlyRpcParams());
    }

    [ClientRpc]
    private void StartLocalBombaSprayOwnerClientRpc(
        Vector3 spawnPos,
        Vector3 direction,
        float force,
        float radius,
        float duration,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
            return;

        HabilidadeBombaSpray ability = ResolveAbilityOfType<HabilidadeBombaSpray>();
        if (ability?.projectilePrefab == null)
            return;

        BombaSprayProjectile bomba = Object.Instantiate(
            ability.projectilePrefab,
            spawnPos,
            Quaternion.LookRotation(direction));
        bomba.LaunchVisualProxy(direction * force, radius, duration);
    }

    public void StartLocalPosturaBaluarteOwnerProxy(float duration)
    {
        HabilidadePosturaBaluarte ability = ResolveAbilityOfType<HabilidadePosturaBaluarte>();
        if (ability == null || !CanSendOwnerOnlyAbilityProxy())
            return;

        StartLocalPosturaBaluarteOwnerClientRpc(duration, BuildOwnerOnlyRpcParams());
    }

    [ClientRpc]
    private void StartLocalPosturaBaluarteOwnerClientRpc(
        float duration,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
            return;

        HabilidadePosturaBaluarte ability = ResolveAbilityOfType<HabilidadePosturaBaluarte>();
        if (ability?.logicPrefab == null)
            return;

        PosturaBaluarteLogic logic = Object.Instantiate(ability.logicPrefab, transform);
        logic.SetupProxy(duration);
    }

    [ServerRpc]
    private void RequestActivateUltimateServerRpc()
    {
        if (!EnsureCharacterDataInitialized())
            return;

        if (characterData.ultimate == null || CurrentUltimateCharge < 1f)
            return;

        bool shouldStartCooldown = characterData.ultimate.Activate(gameObject);

        if (shouldStartCooldown)
        {
            abilityCooldowns[characterData.ultimate] = characterData.ultimate.cooldown;
            netUltimateCharge.Value = 0f;
            ActivateUltimateVisualClientRpc();
        }
    }

    [ClientRpc]
    private void ActivateUltimateVisualClientRpc()
    {
        // Feedback visual da Ultimate 
        // Nota: No caso da Coruja, o CacadoraNoturnaLogic jÃƒÂ¡ dispara o trigger de animaÃƒÂ§ÃƒÂ£o no OnNetworkSpawn
    }

    private void HandleDamageDealt(float damage)
    {
        if (!IsOwner || !EnsureCharacterDataInitialized())
            return;

        if (characterData.ultimateChargePerDamage > 0)
            AddUltimateChargeServerRpc(damage * characterData.ultimateChargePerDamage);
    }

    [ServerRpc]
    private void AddUltimateChargeServerRpc(float amount)
    {
        netUltimateCharge.Value = Mathf.Min(netUltimateCharge.Value + amount, ultimateChargeThreshold);
    }

    public void RefundCooldown(string keyword)
    {
        if (characterData == null)
            return;

        if (characterData.ability1 != null && characterData.ability1.name.Contains(keyword))
            abilityCooldowns[characterData.ability1] = 0f;
        else if (characterData.ability2 != null && characterData.ability2.name.Contains(keyword))
            abilityCooldowns[characterData.ability2] = 0f;
    }

    public float GetRemainingCooldownPercent(Ability ability)
    {
        if (ability == null || !abilityCooldowns.ContainsKey(ability) || ability.cooldown <= 0)
            return 0f;
        return abilityCooldowns[ability] / ability.cooldown;
    }

    public void ReduceAllAbilityCooldowns(float reductionAmount)
    {
        List<Ability> keys = new List<Ability>(abilityCooldowns.Keys);
        foreach (Ability ability in keys)
        {
            if (abilityCooldowns[ability] > 0)
                abilityCooldowns[ability] = Mathf.Max(0, abilityCooldowns[ability] - reductionAmount);
        }
    }

    public void ResetCooldown()
    {
        if (characterData == null)
            return;

        if (characterData.ability1 != null && abilityCooldowns.ContainsKey(characterData.ability1))
            abilityCooldowns[characterData.ability1] = 0f;

        if (characterData.ability2 != null && abilityCooldowns.ContainsKey(characterData.ability2))
            abilityCooldowns[characterData.ability2] = 0f;
    }

    public void ResetCooldown(Ability ability)
    {
        if (ability != null && abilityCooldowns.ContainsKey(ability))
            abilityCooldowns[ability] = 0f;
    }

    public void SetAbilityUsage(Ability ability, bool inUse)
    {
        if (ability == null)
            return;

        if (inUse)
            abilityCooldowns[ability] = float.MaxValue;
    }

    private bool EnsureCharacterDataInitialized()
    {
        if (characterData == null)
        {
            NetworkGameplayResolver.TryResolveCharacterData(
                this,
                out characterData,
                allowOwnerLocalFallback: IsOwner);
        }

        if (characterData == null)
            return false;

        if (abilitiesInitialized)
            return true;

        if (characterData.ability1 != null)
        {
            abilityCooldowns[characterData.ability1] = 0f;
            characterData.ability1.Initialize();
        }

        if (characterData.ability2 != null)
        {
            abilityCooldowns[characterData.ability2] = 0f;
            characterData.ability2.Initialize();
        }

        if (characterData.ultimate != null)
        {
            abilityCooldowns[characterData.ultimate] = 0f;
            characterData.ultimate.Initialize();
        }

        if (!passiveEquipped && characterData.passive != null)
        {
            characterData.passive.OnEquip(gameObject);
            passiveEquipped = true;
        }

        abilitiesInitialized = true;
        return true;
    }
}
