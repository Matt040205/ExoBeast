using System.Collections.Generic;
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
    private readonly HashSet<Ability> deferredCooldownAbilities = new HashSet<Ability>();

    // OPTIMIZATION (Sprint 4 / Item G7 - 2026-05-21): cache reutilizavel para iterar
    // abilityCooldowns.Keys sem alocar lista nova por frame.
    // Antes: new List<Ability>(abilityCooldowns.Keys) em 2 hot paths -> ~480 alocacoes/s em 4 jogadores.
    // Agora: cache compartilhado entre Update (cooldown tick) e ReduceAllAbilityCooldowns
    // (sem reentrancia validada) -> zero alocacao por frame.
    // Sem isso: ~600KB/min de garbage collection durante combate ativo.
    private readonly List<Ability> _cooldownKeysCache = new List<Ability>(8);

    public float ultimateChargeThreshold = 100f;

    public NetworkVariable<float> netUltimateCharge = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Acumulador local de carga por segundo — só escreve em netUltimateCharge.Value
    // quando acumular >=1 unidade. Reduz ~30-60 NetworkVariable updates/s para ~1/s
    // durante a carga passiva, sem mudanca visivel no HUD (granularidade ja era 1/threshold).
    private float _pendingPassiveCharge;
    private Vector3 lastAbilityAimOrigin;
    private Vector3 lastAbilityAimDirection = Vector3.forward;
    private bool hasLastAbilityAimPayload;
    private bool hasOwnerGroundedForAbilityRequest;
    private bool ownerGroundedForAbilityRequest;

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

    public override void OnDestroy()
    {
        if (IsOwner && playerHealth != null)
            playerHealth.OnDamageDealt -= HandleDamageDealt;

        if (passiveEquipped && characterData != null && characterData.passive != null)
        {
            characterData.passive.OnUnequip(gameObject);
            passiveEquipped = false;
        }

        base.OnDestroy();
    }

    void Update()
    {
        EnsureCharacterDataInitialized();

        if (IsServer &&
            netUltimateCharge.Value < ultimateChargeThreshold &&
            characterData != null &&
            characterData.ultimateChargePerSecond > 0)
        {
            // Acumula localmente; só publica em NetworkVariable em delta >=1 unidade.
            _pendingPassiveCharge += characterData.ultimateChargePerSecond * Time.deltaTime;

            if (_pendingPassiveCharge >= 1f)
            {
                float toApply = Mathf.Floor(_pendingPassiveCharge);
                _pendingPassiveCharge -= toApply;
                netUltimateCharge.Value = Mathf.Min(
                    netUltimateCharge.Value + toApply,
                    ultimateChargeThreshold);
            }
        }

        if (IsServer || IsOwner)
        {
            // OPTIMIZATION (Sprint 4 / Item G7): usar cache reutilizavel + for index para zero-alloc.
            _cooldownKeysCache.Clear();
            foreach (var kvp in abilityCooldowns)
                _cooldownKeysCache.Add(kvp.Key);

            for (int i = 0; i < _cooldownKeysCache.Count; i++)
            {
                Ability ability = _cooldownKeysCache[i];
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

        if (inputBridge.ConsumeAbility1Pressed()) RequestAbilityActivationWithAim(0);
        if (inputBridge.ConsumeAbility2Pressed()) RequestAbilityActivationWithAim(1);
        if (inputBridge.ConsumeUltimatePressed()) RequestUltimateActivationWithAim();
    }

    private void RequestAbilityActivationWithAim(int abilityIndex)
    {
        CaptureCurrentAimPayload(out Vector3 aimOrigin, out Vector3 aimDirection);
        RequestActivateAbilityServerRpc(
            abilityIndex,
            aimOrigin,
            aimDirection,
            CaptureOwnerGroundedForAbilityRequest());
    }

    private void RequestUltimateActivationWithAim()
    {
        CaptureCurrentAimPayload(out Vector3 aimOrigin, out Vector3 aimDirection);
        RequestActivateUltimateServerRpc(aimOrigin, aimDirection);
    }

    [ServerRpc]
    private void RequestActivateAbilityServerRpc(
        int abilityIndex,
        Vector3 aimOrigin,
        Vector3 aimDirection,
        bool ownerGroundedForAbility)
    {
        if (!EnsureCharacterDataInitialized())
            return;

        SetLastAbilityAimPayload(aimOrigin, aimDirection);

        Ability abilityToUse = null;
        if (abilityIndex == 0) abilityToUse = characterData.ability1;
        else if (abilityIndex == 1) abilityToUse = characterData.ability2;

        if (abilityToUse == null)
            return;

        if (abilityCooldowns.ContainsKey(abilityToUse) && abilityCooldowns[abilityToUse] > 0)
            return;

        bool started;
        hasOwnerGroundedForAbilityRequest = true;
        ownerGroundedForAbilityRequest = ownerGroundedForAbility;
        try
        {
            started = abilityToUse.Activate(gameObject);
        }
        finally
        {
            hasOwnerGroundedForAbilityRequest = false;
            ownerGroundedForAbilityRequest = false;
        }

        if (started)
        {
            if (IsAbilityCooldownDeferred(abilityToUse))
                return;

            abilityCooldowns[abilityToUse] = GetModifiedCooldown(abilityToUse);
            GrantInfiniteAmmoAfterAbility();
            ActivateAbilityVisualClientRpc(abilityIndex);
        }
    }

    private bool CaptureOwnerGroundedForAbilityRequest()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        return movement != null && movement.IsGroundedForGameplay(0.75f);
    }

    internal bool TryGetOwnerGroundedForAbilityRequest(out bool grounded)
    {
        grounded = ownerGroundedForAbilityRequest;
        return hasOwnerGroundedForAbilityRequest;
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

        abilityCooldowns[ability] = GetModifiedCooldown(ability);

        // VFX Global para a habilidade Aqui Não (todos os clientes veem)
        if (ability is HabilidadeAquiNao aquiNaoAbility && aquiNaoAbility.slashVfxPrefab != null)
        {
            Quaternion rot = AbilityAimUtility.ResolveAimRotation(gameObject);
            GlobalVFXPool.GetVFX(aquiNaoAbility.slashVfxPrefab, transform.position, rot, 1.5f);
        }
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
        logic.transform.localPosition = Vector3.zero;
        logic.transform.localRotation = Quaternion.identity;
        logic.StartUltimate(gameObject, duration, shotsCount, damagePerShot, radius, silenceDuration, false, ability.ultimateVfxPrefab);
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
            ExoAudioService.PlayOneShot3D(sfxSwing, pos);

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
    private void RequestActivateUltimateServerRpc(Vector3 aimOrigin, Vector3 aimDirection)
    {
        if (!EnsureCharacterDataInitialized())
            return;

        SetLastAbilityAimPayload(aimOrigin, aimDirection);

        if (characterData.ultimate == null || CurrentUltimateCharge < 1f)
            return;

        bool shouldStartCooldown = characterData.ultimate.Activate(gameObject);

        if (shouldStartCooldown)
        {
            // OPTIMIZATION (Sprint 4 / Item E7 - 2026-05-21): ActivateUltimateVisualClientRpc
            // removido. Corpo era vazio (comentario "feedback visual" mas sem implementacao).
            // CacadoraNoturnaLogic (Coruja) ja dispara trigger de animacao em OnNetworkSpawn.
            // Antes: 1 ClientRpc broadcast por ult ativada (3 pacotes inuteis em lobby 4-player).
            // Agora: visual fica a cargo do logic spawnado pela ult — sem RPC vazio.
            abilityCooldowns[characterData.ultimate] = GetModifiedCooldown(characterData.ultimate);
            GrantInfiniteAmmoAfterAbility();
            netUltimateCharge.Value = 0f;
            _pendingPassiveCharge = 0f;
        }
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
        // Flush do acumulador passivo antes de aplicar o evento discreto de dano,
        // para nao perder fração acumulada quando o jogador acerta um critico no meio
        // do tick de carga passiva.
        float total = amount + _pendingPassiveCharge;
        _pendingPassiveCharge = 0f;
        netUltimateCharge.Value = Mathf.Min(netUltimateCharge.Value + total, ultimateChargeThreshold);
    }

    public void RefundCooldown(string keyword)
    {
        if (characterData == null)
            return;

        if (characterData.ability1 != null && characterData.ability1.name.Contains(keyword))
        {
            deferredCooldownAbilities.Remove(characterData.ability1);
            abilityCooldowns[characterData.ability1] = 0f;
        }
        else if (characterData.ability2 != null && characterData.ability2.name.Contains(keyword))
        {
            deferredCooldownAbilities.Remove(characterData.ability2);
            abilityCooldowns[characterData.ability2] = 0f;
        }
    }

    public float GetRemainingCooldownPercent(Ability ability)
    {
        float cooldown = GetModifiedCooldown(ability);
        if (ability == null || !abilityCooldowns.ContainsKey(ability) || cooldown <= 0)
            return 0f;
        return Mathf.Clamp01(abilityCooldowns[ability] / cooldown);
    }

    public void ReduceAllAbilityCooldowns(float reductionAmount)
    {
        // OPTIMIZATION (Sprint 4 / Item G7): cache compartilhado com Update. Safe pois
        // NineTailsDanceAbility e o unico caller e nao reentra durante a iteracao.
        _cooldownKeysCache.Clear();
        foreach (var kvp in abilityCooldowns)
            _cooldownKeysCache.Add(kvp.Key);

        for (int i = 0; i < _cooldownKeysCache.Count; i++)
        {
            Ability ability = _cooldownKeysCache[i];
            if (abilityCooldowns[ability] > 0)
                abilityCooldowns[ability] = Mathf.Max(0, abilityCooldowns[ability] - reductionAmount);
        }
    }

    public void ResetCooldown()
    {
        if (characterData == null)
            return;

        if (characterData.ability1 != null && abilityCooldowns.ContainsKey(characterData.ability1))
        {
            deferredCooldownAbilities.Remove(characterData.ability1);
            abilityCooldowns[characterData.ability1] = 0f;
        }

        if (characterData.ability2 != null && abilityCooldowns.ContainsKey(characterData.ability2))
        {
            deferredCooldownAbilities.Remove(characterData.ability2);
            abilityCooldowns[characterData.ability2] = 0f;
        }
    }

    public void ResetCooldown(Ability ability)
    {
        if (ability != null && abilityCooldowns.ContainsKey(ability))
        {
            deferredCooldownAbilities.Remove(ability);
            abilityCooldowns[ability] = 0f;
        }
    }

    public void SetAbilityUsage(Ability ability, bool inUse)
    {
        if (ability == null)
            return;

        if (inUse)
            abilityCooldowns[ability] = float.MaxValue;
    }

    public void DeferAbilityCooldownUntilReleased(Ability ability)
    {
        if (ability == null)
            return;

        abilityCooldowns[ability] = float.MaxValue;
        deferredCooldownAbilities.Add(ability);

        int abilityIndex = ResolveAbilityIndex(ability);
        if (CanSendCooldownSync(abilityIndex))
            SetAbilityInUseClientRpc(abilityIndex);
    }

    public void StartAbilityCooldown(Ability ability)
    {
        if (ability == null)
            return;

        abilityCooldowns[ability] = GetModifiedCooldown(ability);
        deferredCooldownAbilities.Remove(ability);

        int abilityIndex = ResolveAbilityIndex(ability);
        if (CanSendCooldownSync(abilityIndex))
            SetAbilityCooldownClientRpc(abilityIndex, GetModifiedCooldown(ability));
    }

    public bool TryGetLastAbilityAim(out Vector3 origin, out Vector3 direction)
    {
        origin = lastAbilityAimOrigin;
        direction = lastAbilityAimDirection;
        return hasLastAbilityAimPayload && direction.sqrMagnitude > 0.0001f;
    }

    private void CaptureCurrentAimPayload(out Vector3 origin, out Vector3 direction)
    {
        if (AbilityAimUtility.TryResolveAimPose3D(gameObject, out origin, out direction))
            return;

        origin = transform.position;
        direction = AbilityAimUtility.ResolveAimForward(gameObject);
    }

    private void SetLastAbilityAimPayload(Vector3 origin, Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            hasLastAbilityAimPayload = false;
            return;
        }

        lastAbilityAimOrigin = origin;
        lastAbilityAimDirection = direction.normalized;
        hasLastAbilityAimPayload = true;
    }

    private bool IsAbilityCooldownDeferred(Ability ability)
    {
        return ability != null && deferredCooldownAbilities.Contains(ability);
    }

    private float GetModifiedCooldown(Ability ability)
    {
        return ability != null ? ModificacaoRunState.ApplyAbilityCooldown(ability.cooldown) : 0f;
    }

    private void GrantInfiniteAmmoAfterAbility()
    {
        if (!ModificacaoRunState.IsActive(ModificacaoGameplayEffect.MunicaoInfinitaTemporaria))
            return;

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.GrantInfiniteAmmo(ModificacaoRunState.GetValue(ModificacaoGameplayEffect.MunicaoInfinitaTemporaria, 3f));
    }

    private bool CanSendCooldownSync(int abilityIndex)
    {
        return abilityIndex >= 0 &&
               IsServer &&
               IsSpawned &&
               NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening;
    }

    private int ResolveAbilityIndex(Ability ability)
    {
        if (ability == null || !EnsureCharacterDataInitialized())
            return -1;

        if (ability == characterData.ability1)
            return 0;

        if (ability == characterData.ability2)
            return 1;

        if (ability == characterData.ultimate)
            return 2;

        return -1;
    }

    private Ability ResolveAbilityByIndex(int abilityIndex)
    {
        if (!EnsureCharacterDataInitialized())
            return null;

        if (abilityIndex == 0)
            return characterData.ability1;

        if (abilityIndex == 1)
            return characterData.ability2;

        if (abilityIndex == 2)
            return characterData.ultimate;

        return null;
    }

    [ClientRpc]
    private void SetAbilityInUseClientRpc(int abilityIndex)
    {
        Ability ability = ResolveAbilityByIndex(abilityIndex);
        if (ability != null)
            abilityCooldowns[ability] = float.MaxValue;
    }

    [ClientRpc]
    private void SetAbilityCooldownClientRpc(int abilityIndex, float cooldown)
    {
        Ability ability = ResolveAbilityByIndex(abilityIndex);
        if (ability != null)
            abilityCooldowns[ability] = Mathf.Max(0f, cooldown);
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
