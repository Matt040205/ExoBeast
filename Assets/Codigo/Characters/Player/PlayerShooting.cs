using FMODUnity;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.GameServer;
using ExoBeasts.Multiplayer.Lobby;
using ExoBeasts.Multiplayer.Sync;

/// <summary>
/// Sistema de tiro do jogador.
/// O owner cuida do input e da resposta visual imediata; o servidor valida o disparo e o dano.
/// </summary>
[RequireComponent(typeof(PlayerHealthSystem))]
public class PlayerShooting : NetworkBehaviour
{
    [Header("Configuracoes")]
    public CharacterBase characterData;
    public Transform firePoint;
    public GameObject projectileVisualPrefab;
    public GameObject impactEffectPrefab;
    public GameObject muzzleFlashPrefab;

    [Header("Explosao em Area (Coruja)")]
    public GameObject explosionVfxPrefab;
    public string explosionVfxRadiusParam = "Radius";

    [Header("Juice Configs")]
    [SerializeField] private CameraShakeConfig empoweredShotShake = new CameraShakeConfig(3f, 0.5f, 0.3f);

    [Header("Configuracoes de IK (Rigging)")]
    public Transform aimTarget;
    public float aimTargetDistance = 20f;

    [Header("Configuracoes FMOD")]
    [Tooltip("Escreva 'Arma' ou 'Arco'")]
    public string tipoDeSom = "Arma";

    [Header("FMOD - Sons")]
    [EventRef] public string eventoTiroUnicoArma = "event:/SFX/Atirar";
    [EventRef] public string eventoTiroContinuoArma = "event:/SFX/Atirar_segurando";
    [EventRef] public string eventoRecargaArma = "event:/SFX/Recarga Arma";
    [EventRef] public string eventoTiroUnicoArco = "event:/SFX/Player/Bow_Shot";
    [EventRef] public string eventoTiroContinuoArco = "event:/SFX/Player/Bow_Shot";

    [Header("Raycast Settings")]
    public float maxDistance = 100f;
    public LayerMask hitLayers;

    [Header("Estado")]
    public int currentAmmo;
    public int maxAmmo;
    public bool isReloading;
    public bool isFiring;
    public float reloadStartTime;

    private float nextShotTime;
    private CameraController cameraController;
    private Transform modelPivot;
    private Camera mainCamera;
    private UniversalCharacterAnimator universalAnimator;
    private PlayerHealthSystem playerHealth;
    private PlayerCombatManager combatManager;
    private LocalPlayerInputBridge inputBridge;

    private bool hasNextShotBonus;
    private float nextShotDamageBonus = 1f;
    private float nextShotAreaBonus = 1f;
    private bool fireInputHeld;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner && !IsServer)
        {
            enabled = false;
            return;
        }

        InitializeShooting();

        if (IsOwner && PlayerHUD.Instance != null && playerHealth != null)
            PlayerHUD.Instance.RegistrarJogador(playerHealth);
    }

    private void InitializeShooting()
    {
        EnsureCharacterDataResolved();

        maxAmmo = characterData != null ? characterData.magazineSize : 10;
        if (currentAmmo <= 0 || currentAmmo > maxAmmo)
            currentAmmo = maxAmmo;

        mainCamera = Camera.main;
        playerHealth = GetComponent<PlayerHealthSystem>();
        combatManager = GetComponent<PlayerCombatManager>();
        inputBridge = GetComponent<LocalPlayerInputBridge>();

        PlayerMovement playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            modelPivot = playerMovement.GetModelPivot();
        }

        universalAnimator = GetComponent<UniversalCharacterAnimator>();
        if (universalAnimator == null)
            universalAnimator = GetComponentInChildren<UniversalCharacterAnimator>();

        cameraController = GetComponentInChildren<CameraController>();
        if (cameraController == null && mainCamera != null)
            cameraController = mainCamera.GetComponent<CameraController>();

        if (firePoint == null)
            firePoint = transform;
    }

    private CharacterBase ResolveCharacterDataFromNetworkState(int preferredIndex = -1)
    {
        return NetworkGameplayResolver.ResolveCharacterData(
            this,
            preferredIndex,
            allowOwnerLocalFallback: IsOwner);
    }

    public void OnFire(InputAction.CallbackContext ctx)
    {
        // Removido para evitar duplo acionamento por eventos. 
        // O input é processado exclusivamente por Polling em SyncOwnerInputFromBridge().
    }

    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || !enabled || UsesPolledInput())
            return;

        if (ctx.performed && !isReloading && currentAmmo < maxAmmo)
            RequestReloadServerRpc();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        EnsureCharacterDataResolved();
        SyncOwnerInputFromBridge();

        if (PauseControl.isPaused || BuildManager.isBuildingMode)
            return;

        UpdateAimTargetPosition();

        if (isReloading)
            return;

        HandleShootingLogic();

        if (!UsesPolledInput() && Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo)
            RequestReloadServerRpc();
    }

    private void SyncOwnerInputFromBridge()
    {
        if (!UsesPolledInput())
            return;

        if (characterData != null && characterData.fireMode != FireMode.FullAuto)
        {
            if (inputBridge.ConsumeFirePressed())
            {
                fireInputHeld = true;
            }
        }
        else
        {
            fireInputHeld = inputBridge.FireHeld;
        }

        if (inputBridge.ConsumeReloadPressed() && !isReloading && currentAmmo < maxAmmo)
            RequestReloadServerRpc();
    }

    private bool UsesPolledInput()
    {
        if (inputBridge == null)
            inputBridge = GetComponent<LocalPlayerInputBridge>();

        return inputBridge != null && inputBridge.isActiveAndEnabled;
    }

    private void HandleShootingLogic()
    {
        if (!fireInputHeld || Time.time < nextShotTime)
            return;

        if (currentAmmo > 0)
        {
            Shoot();
            if (characterData != null && characterData.fireMode != FireMode.FullAuto)
                fireInputHeld = false;
        }
        else
        {
            RequestReloadServerRpc();
        }
    }

    private void UpdateAimTargetPosition()
    {
        if (aimTarget == null)
            return;

        if (!TryBuildAimRay(out Ray ray))
            return;

        Vector3 targetPosition = ray.origin + ray.direction * maxDistance;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitLayers))
            targetPosition = hit.point;

        aimTarget.position = Vector3.Lerp(aimTarget.position, targetPosition, Time.deltaTime * 20f);
    }

    public void SetNextShotBonus(float damageBonus, float areaBonus)
    {
        hasNextShotBonus = true;
        nextShotDamageBonus = damageBonus;
        nextShotAreaBonus = areaBonus;

        if (IsOwner && !IsServer)
            SyncNextShotBonusServerRpc(damageBonus, areaBonus);
    }

    [ServerRpc]
    private void SyncNextShotBonusServerRpc(float damageBonus, float areaBonus)
    {
        hasNextShotBonus = true;
        nextShotDamageBonus = damageBonus;
        nextShotAreaBonus = areaBonus;
    }

    private void Shoot()
    {
        PlayerMovement playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.FaceCameraImmediately();
        }

        Vector3 shotDirection = GetShotDirection();
        Vector3 shotOrigin = firePoint != null ? firePoint.position : transform.position;
        int characterIndex = ResolveCharacterLibraryIndex(characterData);
        bool empoweredShot = hasNextShotBonus;

        ExecuteShootVisual(shotOrigin, shotDirection, true, empoweredShot);
        RequestShootServerRpc(shotOrigin, shotDirection, characterIndex);

        float attackSpeed = characterData != null && characterData.attackSpeed > 0f ? characterData.attackSpeed : 1f;
        nextShotTime = Time.time + (1f / attackSpeed);
        currentAmmo--;
        ConsumeNextShotBonusLocal();

        if (currentAmmo <= 0)
            RequestReloadServerRpc();
    }

    [ServerRpc]
    private void RequestShootServerRpc(Vector3 origin, Vector3 direction, int characterIndex, ServerRpcParams rpcParams = default)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        if (!EnsureServerCharacterData(characterIndex))
            return;

        if (combatManager == null)
            combatManager = GetComponent<PlayerCombatManager>();

        if (combatManager != null && combatManager.netCombatType.Value != CombatType.Ranged)
            return;

        if (!IsOwner)
        {
            if (isReloading || currentAmmo <= 0 || Time.time < nextShotTime)
                return;

            float attackSpeed = characterData != null && characterData.attackSpeed > 0f ? characterData.attackSpeed : 1f;
            nextShotTime = Time.time + (1f / attackSpeed);
            currentAmmo--;
        }

        direction.Normalize();

        float damage = CalculateAuthoritativeDamage(out bool isCritical, out float areaRadius);
        float armorPenetration = characterData != null ? characterData.armorPenetration : 0f;

        SpawnServerProjectile(origin, direction, damage, isCritical, armorPenetration, areaRadius > 0f, areaRadius, rpcParams.Receive.SenderClientId);
        ShootVisualClientRpc(origin, direction);

        if (!IsOwner && currentAmmo <= 0 && !isReloading)
            ReloadClientRpc();
    }

    [ClientRpc]
    private void ShootVisualClientRpc(Vector3 origin, Vector3 direction)
    {
        if (IsOwner)
            return;

        ExecuteShootVisual(origin, direction, false, false);
    }

    private void ExecuteShootVisual(Vector3 origin, Vector3 direction, bool isOwnerShot, bool empoweredShot)
    {
        if (isOwnerShot && universalAnimator != null)
            universalAnimator.TriggerAction(CharacterActionID.Shoot);

        PlayShootSound(origin);

        Vector3 spawnPosition = firePoint != null ? firePoint.position : origin;
        Quaternion spawnRotation = Quaternion.LookRotation(direction);

        if (firePoint != null)
            firePoint.rotation = spawnRotation;

        if (muzzleFlashPrefab != null)
        {
            GameObject flash = GlobalVFXPool.GetVFX(muzzleFlashPrefab, spawnPosition, spawnRotation, 1.5f);
            if (flash != null && firePoint != null)
                flash.transform.SetParent(firePoint);
        }

        if (projectileVisualPrefab != null)
        {
            GameObject visualProjectile = Instantiate(projectileVisualPrefab, spawnPosition, spawnRotation);
            ProjectileVisual visualScript = visualProjectile.GetComponent<ProjectileVisual>();
            if (visualScript != null)
                visualScript.InitializeVisual(direction, transform);
        }

        if (isOwnerShot && tipoDeSom == "Arco" && empoweredShot)
        {
            JuiceEvents.OnCameraShake?.Invoke(
                -direction,
                empoweredShotShake.amplitude,
                empoweredShotShake.frequency,
                empoweredShotShake.duration);
        }
    }

    private void PlayShootSound(Vector3 emissionPosition)
    {
        string eventToPlay = string.Empty;
        bool isFullAuto = characterData != null && characterData.fireMode == FireMode.FullAuto;

        if (tipoDeSom == "Arco")
            eventToPlay = isFullAuto ? eventoTiroContinuoArco : eventoTiroUnicoArco;
        else
            eventToPlay = isFullAuto ? eventoTiroContinuoArma : eventoTiroUnicoArma;

        if (!string.IsNullOrEmpty(eventToPlay))
            RuntimeManager.PlayOneShot(eventToPlay, emissionPosition);
    }

    private float CalculateAuthoritativeDamage(out bool isCritical, out float areaRadius)
    {
        float baseDamage = characterData != null ? characterData.damage : 10f;
        if (BuildManager.Instance != null)
        {
            baseDamage += BuildManager.Instance.GetSynergyVectorDmgBonus();
        }

        float finalDamage = baseDamage;
        isCritical = false;
        areaRadius = 0f;

        if (characterData != null && Random.value <= characterData.critChance)
        {
            finalDamage *= characterData.critDamage;
            isCritical = true;
        }

        if (playerHealth != null)
            finalDamage *= playerHealth.damageMultiplier.Value;

        if (hasNextShotBonus)
        {
            finalDamage *= nextShotDamageBonus;
            areaRadius = nextShotAreaBonus;
            ConsumeNextShotBonusLocal();
        }

        return finalDamage;
    }

    private void ConsumeNextShotBonusLocal()
    {
        hasNextShotBonus = false;
        nextShotDamageBonus = 1f;
        nextShotAreaBonus = 1f;
    }

    public void NotifyConfirmedDamageServer(float damageAmount)
    {
        if (!IsServer || damageAmount <= 0f)
            return;

        ClientRpcParams targetOwner = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };

        ConfirmDamageDealtClientRpc(damageAmount, targetOwner);
    }

    [ClientRpc]
    private void ConfirmDamageDealtClientRpc(float damageAmount, ClientRpcParams clientRpcParams = default)
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealthSystem>();

        playerHealth?.TriggerDamageDealt(damageAmount);
    }

    public void BroadcastExplosionVfxFromServer(Vector3 position, float radius)
    {
        if (!IsServer)
            return;

        PlayExplosionVfxAuthoritativeClientRpc(position, radius);
    }

    [ClientRpc]
    private void PlayExplosionVfxAuthoritativeClientRpc(Vector3 position, float radius)
    {
        PlayExplosionLocal(position, radius);
    }

    private void SpawnServerProjectile(
        Vector3 origin,
        Vector3 direction,
        float damage,
        bool isCritical,
        float armorPenetration,
        bool empoweredShot,
        float explosionRadius,
        ulong attackerClientId)
    {
        if (projectileVisualPrefab == null)
            return;

        GameObject serverProjectileObject = Instantiate(projectileVisualPrefab, origin, Quaternion.LookRotation(direction));
        ProjectileVisual projectileVisual = serverProjectileObject.GetComponent<ProjectileVisual>();
        if (projectileVisual != null)
            projectileVisual.enabled = false;

        float projectileSpeed = projectileVisual != null ? projectileVisual.speed : 80f;
        float projectileLifetime = projectileVisual != null ? projectileVisual.maxLifetime : 2f;

        ServerAuthoritativeProjectile authoritativeProjectile =
            serverProjectileObject.GetComponent<ServerAuthoritativeProjectile>();
        if (authoritativeProjectile == null)
            authoritativeProjectile = serverProjectileObject.AddComponent<ServerAuthoritativeProjectile>();

        authoritativeProjectile.Initialize(
            this,
            attackerClientId,
            damage,
            isCritical,
            armorPenetration,
            direction,
            projectileSpeed,
            projectileLifetime,
            empoweredShot,
            explosionRadius);
    }

    private bool EnsureServerCharacterData(int characterIndex)
    {
        return EnsureCharacterDataResolved(characterIndex);
    }

    public void RequestDamageOnEnemy(ulong enemyNetworkObjectId, float damage, float armorPenetration, bool isCritical)
    {
        if (!IsOwner)
            return;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject enemyNetObj))
            return;

        NetworkedEnemy networkedEnemy = enemyNetObj.GetComponent<NetworkedEnemy>();
        if (networkedEnemy != null && networkedEnemy.IsSpawned)
            networkedEnemy.TakeDamageServerRpc(damage, armorPenetration, isCritical);
    }

    [ServerRpc]
    private void RequestReloadServerRpc()
    {
        ReloadClientRpc();
    }

    [ClientRpc]
    private void ReloadClientRpc()
    {
        StartReloadLocal();
    }

    private void StartReloadLocal()
    {
        if (isReloading)
            return;

        float reloadSpeed = characterData != null && characterData.reloadSpeed > 0f ? characterData.reloadSpeed : 2f;
        float multiplier = 3.0f / reloadSpeed;

        if (universalAnimator != null)
        {
            universalAnimator.SetReloadSpeedMultiplier(multiplier);
            universalAnimator.TriggerAction(CharacterActionID.Reload);
        }

        if (tipoDeSom == "Arma" && !string.IsNullOrEmpty(eventoRecargaArma))
            RuntimeManager.PlayOneShot(eventoRecargaArma, transform.position);

        isReloading = true;
        reloadStartTime = Time.time;

        Invoke(nameof(FinishReload), reloadSpeed);
    }

    private void FinishReload()
    {
        currentAmmo = maxAmmo;
        isReloading = false;
    }

    private void TryResolveAimingReferences()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (cameraController == null)
            cameraController = GetComponentInChildren<CameraController>();

        if (modelPivot == null)
        {
            PlayerMovement playerMovement = GetComponent<PlayerMovement>();
            if (playerMovement != null)
                modelPivot = playerMovement.GetModelPivot();
        }

        if (aimTarget == null || !aimTarget.IsChildOf(transform))
        {
            PlayerMovement playerMovement = GetComponent<PlayerMovement>();
            if (playerMovement != null)
                aimTarget = playerMovement.aimTarget;
        }

        if (firePoint == null)
            firePoint = transform;
    }

    private bool TryBuildAimRay(out Ray ray)
    {
        TryResolveAimingReferences();

        if (mainCamera != null)
        {
            ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return true;
        }

        if (cameraController != null)
        {
            ray = new Ray(cameraController.transform.position, cameraController.transform.forward);
            return true;
        }

        ray = default;
        return false;
    }

    private Vector3 GetFallbackShotDirection()
    {
        TryResolveAimingReferences();

        Transform fallbackTransform = modelPivot != null ? modelPivot : firePoint;
        if (fallbackTransform == null)
            fallbackTransform = transform;

        Vector3 fallbackDirection = fallbackTransform.forward;
        return fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : transform.forward;
    }

    public bool TryGetShotPose(out Vector3 origin, out Vector3 direction)
    {
        TryResolveAimingReferences();

        origin = firePoint != null ? firePoint.position : transform.position;
        direction = GetShotDirection();
        return direction.sqrMagnitude > 0.0001f;
    }

    private Vector3 GetShotDirection()
    {
        TryResolveAimingReferences();

        if (firePoint == null)
            return GetFallbackShotDirection();

        if (!TryBuildAimRay(out Ray ray))
        {
            if (aimTarget != null)
            {
                Vector3 aimTargetDirection = aimTarget.position - firePoint.position;
                if (aimTargetDirection.sqrMagnitude > 0.0001f)
                    return aimTargetDirection.normalized;
            }

            return GetFallbackShotDirection();
        }

        // Ponto virtual no mundo aonde a mira aponta (mesmo padrão de UpdateAimTargetPosition).
        // Se o raycast acertar algo, usa o ponto de impacto real.
        // Se não acertar (hitLayers vazia, ambiente fora do range, etc), usa o ponto
        // mais distante ao longo do raio da câmera — nunca o forward da câmera cru,
        // que ignora o parallax entre câmera e firePoint e faz o tiro sair deslocado.
        Vector3 targetPoint = ray.origin + ray.direction * maxDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitLayers))
            targetPoint = hit.point;

        Vector3 shotDirection = targetPoint - firePoint.position;
        if (shotDirection.sqrMagnitude <= 0.0001f)
            return GetFallbackShotDirection();

        return shotDirection.normalized;
    }

    public float GetRemainingReloadTime()
    {
        if (!isReloading)
            return 0f;

        float reloadSpeed = characterData != null ? characterData.reloadSpeed : 2f;
        return reloadSpeed - (Time.time - reloadStartTime);
    }

    public void RequestExplosionVfx(Vector3 position, float radius)
    {
        if (!IsOwner)
            return;

        PlayExplosionLocal(position, radius);
        RequestExplosionVfxServerRpc(position, radius);
    }

    [ServerRpc]
    private void RequestExplosionVfxServerRpc(Vector3 position, float radius)
    {
        PlayExplosionVfxClientRpc(position, radius);
    }

    [ClientRpc]
    private void PlayExplosionVfxClientRpc(Vector3 position, float radius)
    {
        if (IsOwner)
            return;

        PlayExplosionLocal(position, radius);
    }

    private void PlayExplosionLocal(Vector3 position, float radius)
    {
        // Se este cliente foi quem atirou (owner), aplica o ScreenShake baseado na força da explosão
        if (IsOwner)
        {
            float shakeAmp = Mathf.Clamp(radius * 0.4f, 1f, 4f);
            JuiceEvents.OnCameraShake?.Invoke(Vector3.down, shakeAmp, 12f, 0.25f);
        }

        if (explosionVfxPrefab == null)
            return;

        GameObject vfx = GlobalVFXPool.GetVFX(explosionVfxPrefab, position, Quaternion.identity, 3f);
        if (vfx == null)
            return;

        UnityEngine.VFX.VisualEffect vfxGraph = vfx.GetComponent<UnityEngine.VFX.VisualEffect>();
        if (vfxGraph != null && vfxGraph.HasFloat(explosionVfxRadiusParam))
            vfxGraph.SetFloat(explosionVfxRadiusParam, radius);
        else
            vfx.transform.localScale = Vector3.one * radius;
    }

    private int ResolveCharacterLibraryIndex(CharacterBase character)
    {
        if (character == null || GameDataManager.Instance?.bibliotecaOriginalPersonagens == null)
            return -1;

        string cleanName = character.name.Replace("(Clone)", "");
        return GameDataManager.Instance.bibliotecaOriginalPersonagens.FindIndex(
            item => item != null && item.name == cleanName);
    }

    private bool EnsureCharacterDataResolved(int preferredIndex = -1)
    {
        if (characterData == null)
            characterData = ResolveCharacterDataFromNetworkState(preferredIndex);

        if (characterData == null)
            return false;

        int resolvedMaxAmmo = characterData.magazineSize > 0 ? characterData.magazineSize : 10;
        if (maxAmmo != resolvedMaxAmmo)
            maxAmmo = resolvedMaxAmmo;

        if (currentAmmo <= 0 || currentAmmo > maxAmmo)
            currentAmmo = maxAmmo;

        return true;
    }
}
