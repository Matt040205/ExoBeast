using System.Collections;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Sync;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class DragonDefensiveStanceController : NetworkBehaviour
{
    public NetworkVariable<bool> IsActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerHealthSystem playerHealth;
    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;
    private PlayerCombatManager combatManager;
    private LocalPlayerInputBridge inputBridge;
    private PlayerInput playerInput;
    private CommanderAbilityController abilityController;
    private CommanderAbilityController activeAbilityController;
    private Ability activeAbility;

    private Coroutine activeRoutine;
    private Coroutine tauntRoutine;
    private bool localActiveState;
    private bool serverHooksApplied;
    private bool ownerGameplaySuppressed;

    private float tauntRadius;
    private float tauntTickInterval;
    private ulong ownerClientId;
    private PlayerHealthSystem ownerHealth;

    private GameObject currentShieldVfx;

    private bool movementWasEnabled;
    private bool shootingWasEnabled;
    private bool combatWasEnabled;
    private bool inputBridgeWasEnabled;
    private bool playerInputWasEnabled;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ResolveDependencies();
        IsActive.OnValueChanged += OnActiveStateChanged;
        SyncServerHooks(IsActive.Value);
        ApplyVisualState(IsActive.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsActive.OnValueChanged -= OnActiveStateChanged;
        SyncServerHooks(false);
        ApplyVisualState(false);
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        SyncServerHooks(false);
        ApplyVisualState(false);
        base.OnDestroy();
    }

    public bool ActivateServer(
        float duration,
        float newTauntRadius,
        float newTauntTickInterval,
        CommanderAbilityController abilityController,
        Ability ability)
    {
        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkSession && !IsServer)
            return false;

        ResolveDependencies();
        if (IsStanceActive())
            return false;

        if (!CanActivateFromGround())
            return false;

        activeAbilityController = abilityController != null ? abilityController : this.abilityController;
        activeAbility = ability;

        if (activeAbilityController != null)
            activeAbilityController.DeferAbilityCooldownUntilReleased(activeAbility);

        tauntRadius = Mathf.Max(0f, newTauntRadius);
        tauntTickInterval = Mathf.Max(0.1f, newTauntTickInterval);
        NetworkGameplayResolver.TryResolveAttackerFromPlayer(gameObject, out ownerClientId, out ownerHealth);

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        SetActiveState(true);
        activeRoutine = StartCoroutine(ActiveRoutine(duration));
        return true;
    }

    private void LateUpdate()
    {
        if (currentShieldVfx != null && IsStanceActive())
            UpdateShieldVfxTransform();
    }

    private IEnumerator ActiveRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        SetActiveState(false);
        activeAbilityController?.StartAbilityCooldown(activeAbility);
        activeAbilityController = null;
        activeAbility = null;
        activeRoutine = null;
    }

    private IEnumerator TauntRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(tauntTickInterval);

        while (IsStanceActive())
        {
            ApplyTauntPulse();
            yield return wait;
        }

        tauntRoutine = null;
    }

    private void ApplyTauntPulse()
    {
        if (tauntRadius <= 0f)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, tauntRadius);
        HashSet<EnemyController> tauntedEnemies = new HashSet<EnemyController>();

        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || enemy.IsDead || !tauntedEnemies.Add(enemy))
                continue;

            enemy.ApplyTaunt(transform, tauntTickInterval + 0.1f);
        }
    }

    private void HandleServerDamageTaken(float finalDamage, Transform attacker, bool isMelee, ulong attackerClientId)
    {
        if (!IsStanceActive() || finalDamage <= 0f)
            return;

        playerHealth?.Heal(finalDamage);

        if (attacker == null)
            return;

        EnemyHealthSystem enemyHealth = attacker.GetComponentInParent<EnemyHealthSystem>();
        if (enemyHealth == null)
            return;

        enemyHealth.ApplyAuthoritativeDamage(finalDamage, 0f, false, ownerClientId, ownerHealth);
    }

    private bool IsStanceActive()
    {
        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        return isNetworkSession && IsSpawned ? IsActive.Value : localActiveState;
    }

    private void SetActiveState(bool isActive)
    {
        localActiveState = isActive;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned && IsServer)
            IsActive.Value = isActive;

        SyncServerHooks(isActive);
        ApplyVisualState(isActive);
    }

    private void SyncServerHooks(bool isActive)
    {
        bool shouldManageServerHooks = (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            ? true
            : IsServer;

        if (!shouldManageServerHooks)
            return;

        ResolveDependencies();

        if (isActive)
        {
            if (!serverHooksApplied && playerHealth != null)
            {
                playerHealth.OnServerDamageTaken += HandleServerDamageTaken;
                serverHooksApplied = true;
            }

            if (tauntRoutine == null)
                tauntRoutine = StartCoroutine(TauntRoutine());

            return;
        }

        if (serverHooksApplied && playerHealth != null)
        {
            playerHealth.OnServerDamageTaken -= HandleServerDamageTaken;
            serverHooksApplied = false;
        }

        if (tauntRoutine != null)
        {
            StopCoroutine(tauntRoutine);
            tauntRoutine = null;
        }
    }

    private void OnActiveStateChanged(bool oldValue, bool newValue)
    {
        SyncServerHooks(newValue);
        ApplyVisualState(newValue);
    }

    private void ApplyVisualState(bool isActive)
    {
        ResolveDependencies();

        if (playerHealth != null)
            playerHealth.isCountering = isActive;

        ApplyOwnerGameplaySuppression(isActive);

        // Lógica Visual do Escudo
        if (isActive)
        {
            if (currentShieldVfx == null)
            {
                GameObject shieldPrefab = GetShieldPrefab();
                if (shieldPrefab != null)
                {
                    currentShieldVfx = Instantiate(shieldPrefab, transform.position, transform.rotation, transform);
                    UpdateShieldVfxTransform();
                }
            }
            else
            {
                UpdateShieldVfxTransform();
            }
        }
        else
        {
            if (currentShieldVfx != null)
            {
                Destroy(currentShieldVfx);
                currentShieldVfx = null;
            }
        }
    }

    private bool CanActivateFromGround()
    {
        ResolveDependencies();

        if (playerMovement != null)
            return playerMovement.IsGroundedForGameplay(0.75f);

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null && characterController.enabled)
            return characterController.isGrounded;

        return Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, 1.0f);
    }

    private void UpdateShieldVfxTransform()
    {
        if (currentShieldVfx == null)
            return;

        Vector3 aimForward = AbilityAimUtility.ResolveAimForward(gameObject);
        if (aimForward.sqrMagnitude <= 0.0001f)
            aimForward = transform.forward;

        aimForward.y = 0f;
        if (aimForward.sqrMagnitude <= 0.0001f)
            aimForward = Vector3.forward;

        aimForward.Normalize();
        Vector3 shieldPosition = transform.position + aimForward * 1.0f + Vector3.up * 1f;
        Quaternion shieldRotation = Quaternion.LookRotation(aimForward, Vector3.up);
        currentShieldVfx.transform.SetPositionAndRotation(shieldPosition, shieldRotation);
    }

    private GameObject GetShieldPrefab()
    {
        if (abilityController != null && abilityController.characterData != null)
        {
            if (abilityController.characterData.ability1 is HabilidadePosturaBaluarte b1) return b1.shieldVfxPrefab;
            if (abilityController.characterData.ability2 is HabilidadePosturaBaluarte b2) return b2.shieldVfxPrefab;
            if (abilityController.characterData.ultimate is HabilidadePosturaBaluarte u) return u.shieldVfxPrefab;
        }
        return null;
    }

    private void ApplyOwnerGameplaySuppression(bool isActive)
    {
        if (!IsOwner)
            return;

        ResolveDependencies();

        if (isActive)
        {
            if (ownerGameplaySuppressed)
                return;

            movementWasEnabled = playerMovement != null && playerMovement.enabled;
            shootingWasEnabled = playerShooting != null && playerShooting.enabled;
            combatWasEnabled = combatManager != null && combatManager.enabled;
            inputBridgeWasEnabled = inputBridge != null && inputBridge.enabled;
            playerInputWasEnabled = playerInput != null && playerInput.enabled;

            if (movementWasEnabled)
                playerMovement.enabled = false;

            if (shootingWasEnabled)
                playerShooting.enabled = false;

            if (combatWasEnabled)
                combatManager.enabled = false;

            if (inputBridgeWasEnabled)
                inputBridge.enabled = false;

            if (playerInputWasEnabled)
                playerInput.enabled = false;

            ownerGameplaySuppressed = true;
            return;
        }

        if (!ownerGameplaySuppressed)
            return;

        if (playerMovement != null)
            playerMovement.enabled = movementWasEnabled;

        if (playerShooting != null)
            playerShooting.enabled = shootingWasEnabled;

        if (combatManager != null)
            combatManager.enabled = combatWasEnabled;

        if (inputBridge != null)
            inputBridge.enabled = inputBridgeWasEnabled;

        if (playerInput != null)
            playerInput.enabled = playerInputWasEnabled;

        ownerGameplaySuppressed = false;
    }

    private void ResolveDependencies()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealthSystem>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerShooting == null)
            playerShooting = GetComponent<PlayerShooting>();

        if (combatManager == null)
            combatManager = GetComponent<PlayerCombatManager>();

        if (inputBridge == null)
            inputBridge = GetComponent<LocalPlayerInputBridge>();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (abilityController == null)
            abilityController = GetComponent<CommanderAbilityController>();
    }
}
