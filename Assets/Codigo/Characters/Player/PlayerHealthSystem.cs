using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Sync;

public class PlayerHealthSystem : NetworkBehaviour
{
    public CharacterBase characterData;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [Header("Networked Buffs")]
    public NetworkVariable<float> damageMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> speedMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> damageResistance = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool isRegenerating;
    public bool isBuffed;

    [Header("Status de Defesa")]
    public bool isCountering;

    private float timeSinceLastDamage;
    // Acumulador local de regen — só escreve currentHealth.Value quando acumular >=1 HP.
    // Sem isso, escrever todo frame em NetworkVariable enviava ~30-60 deltas/s por jogador
    // durante toda a regeneracao (dezenas de KB/s desperdiçados em variacao invisível).
    private float _pendingRegenAmount;
    private Transform respawnPoint;
    private Coroutine buffCoroutine;
    private Coroutine spawnMaterializationCoroutine;
    private float lastRespawnTime = -999f;

    /// <summary>
    /// Incrementado a cada morte/respawn. Utilizado para cancelar DoTs (Sangramento, etc) que pertenciam à vida anterior.
    /// </summary>
    public int SpawnGeneration { get; private set; } = 0;

    [Header("Configuracao de Respawn")]
    public string respawnPointNameOrTag = "RespawnPoint";

    [Header("Materializacao (Spawn)")]
    [SerializeField] private float tempoDeSpawn = 2f;
    [SerializeField] private Material materialHolograma;

    public event Action OnHealthChanged;
    public event Action<float> OnDamageDealt;
    public event Action<float, Transform, bool, ulong> OnServerDamageTaken;

    /// <summary>
    /// Evento disparado no cliente local quando ele sofre dano, util para atualizar a UI (DamageIndicator).
    /// </summary>
    public event Action<float, Vector3, bool> OnLocalDamageTaken;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        TryResolveCharacterData();

        if (IsServer)
        {
            if (characterData != null)
                currentHealth.Value = characterData.maxHealth;
            else
                Debug.LogWarning("[PlayerHealthSystem] characterData nao atribuido.");
        }

        currentHealth.OnValueChanged += OnCurrentHealthValueChanged;
        NotifyHealthChanged();
        FindRespawnPoint();

        if (IsOwner)
            StartCoroutine(WaitAndRegisterHUD());

        lastRespawnTime = Time.time;
        RestartSpawnMaterializationFlow();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnCurrentHealthValueChanged;
        base.OnNetworkDespawn();
    }

    private IEnumerator WaitAndRegisterHUD()
    {
        float elapsed = 0f;
        while (PlayerHUD.Instance == null && elapsed < 10f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (PlayerHUD.Instance != null)
            PlayerHUD.Instance.RegistrarJogador(this);
    }

    private void Update()
    {
        TryResolveCharacterData();
        if (!IsServer)
            return;

        HandleRegeneration();
    }

    public void ApplyBuffs(float newDamageMult, float newSpeedMult, float duration)
    {
        if (!IsServer)
            return;

        if (buffCoroutine != null)
            StopCoroutine(buffCoroutine);

        damageMultiplier.Value = newDamageMult;
        speedMultiplier.Value = newSpeedMult;
        isBuffed = true;
        buffCoroutine = StartCoroutine(RemoveBuffsAfterTime(duration));
    }

    public void TriggerDamageDealt(float damageAmount)
    {
        OnDamageDealt?.Invoke(damageAmount);
    }

    public void TakeDamage(float damage, Transform attacker = null, bool isMelee = false, ulong attackerClientId = ulong.MaxValue)
    {
        if (!IsServer)
            return;

        // Invulnerabilidade pós-respawn (I-frames) durante o holograma
        if (Time.time - lastRespawnTime < tempoDeSpawn)
        {
            Debug.Log($"[PlayerHealthSystem] Dano ignorado. '{gameObject.name}' esta com invulnerabilidade de respawn.");
            return;
        }

        DamageRequest request = new DamageRequest(damage, attacker, isMelee, attackerClientId);
        DamageResponse response = DamageResponse.PassThrough(damage);
        ResolveDamageInterceptors(ref request, ref response);

        if (response.WasBlocked)
            return;

        float finalDamage = response.ModifiedDamage * (1f - damageResistance.Value);

        AllyShield shield = GetComponent<AllyShield>();
        if (shield != null && shield.IsActive)
            finalDamage = shield.AbsorbDamage(finalDamage);

        DragonAuraBuff auraBuff = GetComponent<DragonAuraBuff>();
        if (auraBuff != null && auraBuff.DamageReduction > 0f)
            finalDamage *= (1f - auraBuff.DamageReduction);

        finalDamage = Mathf.Max(0f, finalDamage);
        currentHealth.Value -= finalDamage;
        timeSinceLastDamage = 0f;
        _pendingRegenAmount = 0f;
        isRegenerating = false;

        if (finalDamage > 0f)
        {
            OnServerDamageTaken?.Invoke(finalDamage, request.Attacker, request.IsMelee, request.AttackerClientId);

            // Avisa o cliente dono sobre o dano recebido e a posicao do atacante
            Vector3 attackerPos = request.Attacker != null ? request.Attacker.position : Vector3.zero;
            bool hasAttacker = request.Attacker != null;
            
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
            };
            NotifyDamageTakenClientRpc(finalDamage, attackerPos, hasAttacker, clientRpcParams);
        }

        if (currentHealth.Value <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (!IsServer)
            return;

        if (characterData != null)
            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, characterData.maxHealth);
    }

    public bool IsAtFullHealth(float tolerance = 0.01f)
    {
        TryResolveCharacterData();
        return characterData != null && currentHealth.Value >= characterData.maxHealth - tolerance;
    }

    public bool CanReceiveHealing(float tolerance = 0.01f)
    {
        return !IsAtFullHealth(tolerance);
    }

    private IEnumerator RemoveBuffsAfterTime(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (IsServer)
        {
            damageMultiplier.Value = 1f;
            speedMultiplier.Value = 1f;
            isBuffed = false;
        }

        buffCoroutine = null;
    }

    private void FindRespawnPoint()
    {
        if (GameSetupManager.TryResolveRespawnTransform(respawnPointNameOrTag, out Transform resolvedRespawnPoint))
            respawnPoint = resolvedRespawnPoint;
    }

    private bool TryResolveRespawnPose(out Vector3 position, out Quaternion rotation)
    {
        if (respawnPoint != null && respawnPoint.gameObject.activeInHierarchy)
        {
            position = respawnPoint.position;
            rotation = respawnPoint.rotation;
            return true;
        }

        if (GameSetupManager.TryResolveRespawnTransform(respawnPointNameOrTag, out Transform resolvedRespawnPoint))
        {
            respawnPoint = resolvedRespawnPoint;
            position = resolvedRespawnPoint.position;
            rotation = resolvedRespawnPoint.rotation;
            return true;
        }

        position = transform.position;
        rotation = transform.rotation;
        return false;
    }

    private void HandleRegeneration()
    {
        if (characterData == null)
            return;

        if (currentHealth.Value >= characterData.maxHealth)
        {
            isRegenerating = false;
            _pendingRegenAmount = 0f;
            return;
        }

        timeSinceLastDamage += Time.deltaTime;
        if (timeSinceLastDamage < 3f)
            return;

        isRegenerating = true;

        // Acumula regen localmente — escreve em NetworkVariable apenas quando o delta
        // for >=1 HP (granularidade da barra de HP). Reduz pacotes de ~30-60/s para
        // ~1/s por jogador em regeneracao, sem mudanca visivel para o usuario.
        _pendingRegenAmount += characterData.maxHealth * 0.01f * Time.deltaTime;

        if (_pendingRegenAmount >= 1f)
        {
            float toApply = Mathf.Floor(_pendingRegenAmount);
            _pendingRegenAmount -= toApply;
            currentHealth.Value = Mathf.Min(currentHealth.Value + toApply, characterData.maxHealth);
        }
    }

    private void Die()
    {
        if (!IsServer)
            return;

        if (!TryResolveRespawnPose(out Vector3 spawnPos, out Quaternion spawnRot))
        {
            spawnPos = transform.position;
            spawnRot = transform.rotation;
            Debug.LogError($"[PlayerHealthSystem] Nenhum respawn valido encontrado para '{name}'. Mantendo posicao atual para evitar Vector3.zero.");
        }

        currentHealth.Value = characterData != null ? characterData.maxHealth : 100f;
        damageMultiplier.Value = 1f;
        speedMultiplier.Value = 1f;
        isCountering = false;
        
        if (buffCoroutine != null)
        {
            StopCoroutine(buffCoroutine);
            buffCoroutine = null;
        }
        isBuffed = false;

        SpawnGeneration++;
        lastRespawnTime = Time.time; // Ativa os i-frames pro respawn

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            PlayerTeleportService.TeleportServerValidated(NetworkObject, spawnPos, spawnRot);

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                ClientRpcParams targetParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { OwnerClientId }
                    }
                };
                RespawnOwnerTeleportClientRpc(spawnPos, spawnRot, targetParams);
            }
        }
        else
        {
            PlayerTeleportService.TeleportLocal(gameObject, spawnPos, spawnRot);
        }

        RespawnVisualClientRpc();
    }

    [ClientRpc]
    private void RespawnOwnerTeleportClientRpc(Vector3 spawnPosition, Quaternion spawnRotation, ClientRpcParams rpcParams = default)
    {
        if (IsOwner)
            PlayerTeleportService.TeleportLocal(gameObject, spawnPosition, spawnRotation);
    }

    [ClientRpc]
    private void RespawnVisualClientRpc()
    {
        RestartSpawnMaterializationFlow();
    }

    [ClientRpc]
    private void NotifyDamageTakenClientRpc(float damage, Vector3 attackerPosition, bool hasAttacker, ClientRpcParams rpcParams = default)
    {
        if (IsOwner)
        {
            OnLocalDamageTaken?.Invoke(damage, attackerPosition, hasAttacker);
        }
    }

    private void RestartSpawnMaterializationFlow()
    {
        if (spawnMaterializationCoroutine != null)
            StopCoroutine(spawnMaterializationCoroutine);

        spawnMaterializationCoroutine = StartCoroutine(SpawnMaterializationFlow());
    }

    private void RestoreOwnerGameplayState(
        CharacterController controller,
        bool controllerWasEnabled,
        PlayerMovement movementScript,
        bool movementWasEnabled,
        PlayerShooting shootingScript,
        bool shootingWasEnabled,
        PlayerCombatManager combatScript,
        bool combatWasEnabled)
    {
        if (!IsOwner)
            return;

        if (controller != null)
            controller.enabled = controllerWasEnabled;

        if (movementScript != null)
            movementScript.enabled = movementWasEnabled;

        if (shootingScript != null)
            shootingScript.enabled = shootingWasEnabled;

        if (combatScript != null)
            combatScript.enabled = combatWasEnabled;
    }

    private IEnumerator SpawnMaterializationFlow()
    {
        CharacterController controller = null;
        PlayerMovement movementScript = null;
        PlayerShooting shootingScript = null;
        PlayerCombatManager combatScript = null;
        bool controllerWasEnabled = false;
        bool movementWasEnabled = false;
        bool shootingWasEnabled = false;
        bool combatWasEnabled = false;

        if (IsOwner)
        {
            controller = GetComponent<CharacterController>();
            movementScript = GetComponent<PlayerMovement>();
            shootingScript = GetComponent<PlayerShooting>();
            combatScript = GetComponent<PlayerCombatManager>();

            controllerWasEnabled = controller != null && controller.enabled;
            movementWasEnabled = movementScript != null && movementScript.enabled;
            shootingWasEnabled = shootingScript != null && shootingScript.enabled;
            combatWasEnabled = combatScript != null && combatScript.enabled;

            if (controllerWasEnabled) controller.enabled = false;
            if (movementWasEnabled) movementScript.enabled = false;
            if (shootingWasEnabled) shootingScript.enabled = false;
            if (combatWasEnabled) combatScript.enabled = false;
        }

        try
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            System.Collections.Generic.List<Renderer> targetRenderers = new System.Collections.Generic.List<Renderer>();
            System.Collections.Generic.Dictionary<Renderer, Material[]> originalMaterials = new System.Collections.Generic.Dictionary<Renderer, Material[]>();

            foreach (Renderer renderer in allRenderers)
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                    continue;

                targetRenderers.Add(renderer);
                originalMaterials[renderer] = renderer.materials; // Salva o array de materiais original
            }

            if (materialHolograma != null)
            {
                foreach (Renderer renderer in targetRenderers)
                {
                    // Se o mesh tem múltiplos materiais (ex: corpo, roupa), preenche todos com o holograma
                    Material[] holoMaterials = new Material[renderer.materials.Length];
                    for (int i = 0; i < holoMaterials.Length; i++)
                        holoMaterials[i] = materialHolograma;
                    renderer.materials = holoMaterials;
                }
            }

            float elapsedTime = 0f;
            while (elapsedTime < tempoDeSpawn)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / tempoDeSpawn);
                float invertedProgress = 1f - progress;

                if (materialHolograma != null)
                {
                    foreach (Renderer renderer in targetRenderers)
                    {
                        foreach (Material mat in renderer.materials)
                        {
                            if (mat.HasProperty("Progresso_Holograma"))
                                mat.SetFloat("Progresso_Holograma", invertedProgress);
                            else if (mat.HasProperty("_Progresso_Holograma"))
                                mat.SetFloat("_Progresso_Holograma", invertedProgress);
                        }
                    }
                }

                yield return null;
            }

            // Restaura os materiais originais exatamente como eram
            foreach (Renderer renderer in targetRenderers)
            {
                if (originalMaterials.TryGetValue(renderer, out Material[] origMats))
                {
                    renderer.materials = origMats;
                }
            }
        }
        finally
        {
            RestoreOwnerGameplayState(
                controller,
                controllerWasEnabled,
                movementScript,
                movementWasEnabled,
                shootingScript,
                shootingWasEnabled,
                combatScript,
                combatWasEnabled);

            spawnMaterializationCoroutine = null;
        }
    }

    private void OnCurrentHealthValueChanged(float oldValue, float newValue)
    {
        NotifyHealthChanged();
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke();
    }

    private void TryResolveCharacterData()
    {
        if (characterData == null)
            NetworkGameplayResolver.TryResolveCharacterData(this, out characterData, allowOwnerLocalFallback: IsOwner);
    }

    private void ResolveDamageInterceptors(ref DamageRequest request, ref DamageResponse response)
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDamageInterceptor interceptor &&
                interceptor.TryIntercept(this, ref request, ref response))
            {
                return;
            }
        }
    }
}
