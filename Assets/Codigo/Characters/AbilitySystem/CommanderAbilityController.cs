using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Lobby;  // LobbyManager — índice de membro do lobby
using ExoBeasts.Multiplayer.Auth;   // SessionManager — productUserId local

/// <summary>
/// ── CommanderAbilityController ─────────────────────────
/// Controlador de habilidades do personagem (Q, E, X).
///
///  ▸ Owner: detecta input e envia RequestActivateAbilityServerRpc
///  ▸ Server: valida cooldown, executa Ability.Activate(), notifica clientes
///  ▸ NetworkVariable netUltimateCharge: carga da ultimate (dano + tempo)
///  ▸ Suporta passiva via characterData.passive.OnEquip()
/// ─────────────────────────────────────────────────────
/// </summary>
public class CommanderAbilityController : NetworkBehaviour
{
    public CharacterBase characterData;
    private Animator anim;
    private PlayerHealthSystem playerHealth;

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

        if (IsOwner && playerHealth != null)
        {
            playerHealth.OnDamageDealt += HandleDamageDealt;
        }

        // ScriptableObjects não viajam pela rede — resolver localmente quando nulo.
        // Segue o mesmo padrão de PlayerShooting.ResolveLocalCommanderCharacter().
        if (characterData == null && GameDataManager.Instance != null)
        {
            characterData = ResolveLocalCommanderCharacter();
            Debug.Log($"[CommanderAbilityController] characterData resolvido via lobby index: {characterData?.name}");
        }

        if (characterData != null)
        {
            if (characterData.ability1 != null) { abilityCooldowns[characterData.ability1] = 0; characterData.ability1.Initialize(); }
            if (characterData.ability2 != null) { abilityCooldowns[characterData.ability2] = 0; characterData.ability2.Initialize(); }
            if (characterData.ultimate != null) { abilityCooldowns[characterData.ultimate] = 0; characterData.ultimate.Initialize(); }

            // Ativa a passiva em todos os clientes (cada um aplica o que lhe cabe: Owner=Input, Server=Damage, All=Visual)
            if (characterData.passive != null)
            {
                characterData.passive.OnEquip(gameObject);
            }
        }
        else
        {
            Debug.LogWarning($"[CommanderAbilityController] characterData ainda nulo após resolução local. Habilidades desabilitadas para clientId={OwnerClientId}.");
        }
    }

    /// <summary>
    /// Resolve o CharacterBase do Comandante deste jogador consultando o índice de
    /// membro no lobby. Duplica a lógica de PlayerShooting para evitar dependência
    /// de componente entre scripts do prefab do jogador.
    ///
    /// Layout: 2p → P0=slot0, P1=slot4 | 3p → P0=0, P1=4, P2=6 | 4p → Px=x*2
    /// </summary>
    private CharacterBase ResolveLocalCommanderCharacter()
    {
        var equipe = GameDataManager.Instance?.equipeSelecionada;
        if (equipe == null || equipe.Length == 0) return null;

        int commanderSlot = 0;

        var lobbyMgr = LobbyManager.Instance;
        var sessionMgr = SessionManager.Instance;

        if (lobbyMgr != null && sessionMgr != null)
        {
            var membros  = lobbyMgr.GetOrderedMembers();
            string meuId = sessionMgr.GetUserId();
            int meuIndice = lobbyMgr.GetCanonicalMemberIndex(meuId);
            int total     = membros.Count;

            if (meuIndice >= 0)
                commanderSlot = PartySlotLayout.GetCommanderSlot(total, meuIndice);

        }

        return (commanderSlot < equipe.Length && equipe[commanderSlot] != null)
            ? equipe[commanderSlot]
            : (equipe.Length > 0 ? equipe[0] : null);
    }

    private T ResolveAbilityOfType<T>() where T : Ability
    {
        if (characterData == null)
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
        {
            playerHealth.OnDamageDealt -= HandleDamageDealt;
        }
        
        if (characterData != null && characterData.passive != null)
        {
            characterData.passive.OnUnequip(gameObject);
        }
    }

    void Update()
    {
        // Acumulacao de carga roda no servidor para TODOS os jogadores (nao apenas o host)
        if (IsServer && netUltimateCharge.Value < ultimateChargeThreshold && characterData != null && characterData.ultimateChargePerSecond > 0)
        {
            netUltimateCharge.Value = Mathf.Min(netUltimateCharge.Value + characterData.ultimateChargePerSecond * Time.deltaTime, ultimateChargeThreshold);
        }

        // Cooldown: roda no servidor (onde o check ocorre) E no owner (para feedback de UI local).
        // CRITICO: sem IsServer aqui, o cooldown de jogadores nao-host nunca decrementa no servidor,
        // bloqueando Q e E permanentemente apos o primeiro uso.
        if (IsServer || IsOwner)
        {
            List<Ability> keys = new List<Ability>(abilityCooldowns.Keys);
            foreach (Ability ability in keys)
            {
                if (abilityCooldowns[ability] > 0)
                {
                    abilityCooldowns[ability] -= Time.deltaTime;
                    if (abilityCooldowns[ability] < 0) abilityCooldowns[ability] = 0;
                }
            }
        }

        // Input: exclusivo do dono local
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Q)) RequestActivateAbilityServerRpc(0);
        if (Input.GetKeyDown(KeyCode.E)) RequestActivateAbilityServerRpc(1);
        if (Input.GetKeyDown(KeyCode.X)) RequestActivateUltimateServerRpc();
    }

    [ServerRpc]
    private void RequestActivateAbilityServerRpc(int abilityIndex)
    {
        Ability abilityToUse = null;
        if (abilityIndex == 0) abilityToUse = characterData.ability1;
        else if (abilityIndex == 1) abilityToUse = characterData.ability2;

        if (abilityToUse == null) return;

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
        if (characterData == null) return; // guard: clientes que nao tem characterData no prefab

        Ability ability = null;
        if (abilityIndex == 0) ability = characterData.ability1;
        else if (abilityIndex == 1) ability = characterData.ability2;

        if (ability == null) return;

        // Sincronizar cooldown em TODOS os clientes, inclusive o owner.
        // Sem isso o Player 2 (owner) nunca sabe que a habilidade entrou em cooldown:
        // abilityCooldowns fica em 0 local, gerando ausencia de feedback e ativacoes inconsistentes.
        abilityCooldowns[ability] = ability.cooldown;
    }

    public void StartLocalMergulhoTintaOwnerProxy(float duration, float exitDamage, float damageRadius)
    {
        HabilidadeMergulhoTinta ability = ResolveAbilityOfType<HabilidadeMergulhoTinta>();
        if (ability == null || !CanSendOwnerOnlyAbilityProxy())
            return;

        StartLocalMergulhoTintaOwnerClientRpc(duration, exitDamage, damageRadius, BuildOwnerOnlyRpcParams());
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
        logic.StartDive(duration, exitDamage, damageRadius, ability.visualPuddlePrefab, ability, false);
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

    [ServerRpc]
    private void RequestActivateUltimateServerRpc()
    {
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
        // Nota: No caso da Coruja, o CacadoraNoturnaLogic jÃ¡ dispara o trigger de animaÃ§Ã£o no OnNetworkSpawn
    }

    private void HandleDamageDealt(float damage)
    {
        if (!IsOwner) return;

        if (characterData != null && characterData.ultimateChargePerDamage > 0)
        {
            AddUltimateChargeServerRpc(damage * characterData.ultimateChargePerDamage);
        }
    }

    [ServerRpc]
    private void AddUltimateChargeServerRpc(float amount)
    {
        netUltimateCharge.Value = Mathf.Min(netUltimateCharge.Value + amount, ultimateChargeThreshold);
    }

    public void RefundCooldown(string keyword)
    {
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
        if (characterData.ability1 != null && abilityCooldowns.ContainsKey(characterData.ability1)) abilityCooldowns[characterData.ability1] = 0f;
        if (characterData.ability2 != null && abilityCooldowns.ContainsKey(characterData.ability2)) abilityCooldowns[characterData.ability2] = 0f;
    }

    public void ResetCooldown(Ability ability)
    {
        if (ability != null && abilityCooldowns.ContainsKey(ability))
            abilityCooldowns[ability] = 0f;
    }

    public void SetAbilityUsage(Ability ability, bool inUse)
    {
        // Marca habilidade como em uso para prevenir re-ativacao durante efeito
        if (ability == null) return;
        if (inUse)
            abilityCooldowns[ability] = float.MaxValue;
    }
}
