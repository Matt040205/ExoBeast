using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// ── PlayerShooting ─────────────────────────────────────
/// Sistema de tiro a distancia com projeteis visuais locais.
///
///  ▸ Owner: detecta input de tiro, spawna projetil visual, envia ShootServerRpc
///  ▸ Server: repassa ShootVisualClientRpc para remotos
///  ▸ Remotos: spawnam projetil visual local (nao eh NetworkObject)
///  ▸ RequestDealDamageServerRpc: dano validado no servidor via NetworkObjectId
///  ▸ Reload sincronizado via ServerRpc → ClientRpc
/// ─────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(PlayerHealthSystem))]
public class PlayerShooting : NetworkBehaviour
{
    [Header("Configurações")]
    public CharacterBase characterData;
    public Transform firePoint;
    public GameObject projectileVisualPrefab;
    public GameObject impactEffectPrefab;

    [Header("Configurações de IK (Rigging)")]
    public Transform aimTarget;
    public float aimTargetDistance = 20f;

    [Header("Configurações FMOD")]
    [Tooltip("Escreva 'Arma' ou 'Arco'")]
    public string tipoDeSom = "Arma";

    [Header("FMOD - Sons")]
    [EventRef] public string eventoTiroUnicoArma = "event:/SFX/Atirar";
    [EventRef] public string eventoTiroContinuoArma = "event:/SFX/Atirar_segurando";
    [EventRef] public string eventoRecargaArma = "event:/SFX/Recarga Arma";
    [EventRef] public string eventoTiroUnicoArco = "event:/SFX/Arco";
    [EventRef] public string eventoTiroContinuoArco = "event:/SFX/Arco";

    [Header("Raycast Settings")]
    public float maxDistance = 100f;
    public LayerMask hitLayers;

    [Header("Estado")]
    public int currentAmmo;
    public bool isReloading;
    public bool isFiring;
    public float reloadStartTime;

    private float nextShotTime;
    private CameraController cameraController;
    private Transform modelPivot;
    private ProjectilePool projectilePool;
    private Camera mainCamera;
    private Animator animator;

    private PlayerHealthSystem playerHealth;
    private bool hasNextShotBonus = false;
    private float nextShotDamageBonus = 1f;
    private float nextShotAreaBonus = 1f;

    private bool fireInputHeld;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }

        InitializeShooting();
    }

    private void InitializeShooting()
    {
        currentAmmo = (characterData != null) ? characterData.magazineSize : 10;
        
        mainCamera = Camera.main;
        playerHealth = GetComponent<PlayerHealthSystem>();

        PlayerMovement playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            modelPivot = playerMovement.GetModelPivot();
            if (modelPivot != null)
                animator = modelPivot.GetComponentInChildren<Animator>();
        }
        
        cameraController = GetComponentInChildren<CameraController>();
        if (cameraController == null && mainCamera != null)
            cameraController = mainCamera.GetComponent<CameraController>();

        projectilePool = ProjectilePool.Instance;
        if (projectilePool != null && projectileVisualPrefab != null)
        {
            projectilePool.projectilePrefab = this.projectileVisualPrefab;
            projectilePool.InitializePool();
        }
    }

    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || !this.enabled) return;

        if (ctx.started || ctx.performed) fireInputHeld = true;
        else if (ctx.canceled) fireInputHeld = false;
    }

    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || !this.enabled) return;

        if (ctx.performed && !isReloading && currentAmmo < characterData.magazineSize)
            RequestReloadServerRpc();
    }

    void Update()
    {
        if (!IsOwner) return;

        if (PauseControl.isPaused || BuildManager.isBuildingMode) return;

        UpdateAimTargetPosition();

        if (isReloading) return;

        HandleShootingLogic();

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < characterData.magazineSize)
            RequestReloadServerRpc();
    }

    void HandleShootingLogic()
    {
        if (fireInputHeld && Time.time >= nextShotTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
                if (characterData.fireMode != FireMode.FullAuto) fireInputHeld = false;
            }
            else RequestReloadServerRpc();
        }
    }

    void UpdateAimTargetPosition()
    {
        if (aimTarget == null || mainCamera == null) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        Vector3 targetPosition;

        if (Physics.Raycast(ray, out hit, maxDistance, hitLayers))
            targetPosition = hit.point;
        else
            targetPosition = ray.origin + ray.direction * maxDistance;

        aimTarget.position = Vector3.Lerp(aimTarget.position, targetPosition, Time.deltaTime * 20f);
    }

    public void SetNextShotBonus(float damageBonus, float areaBonus)
    {
        hasNextShotBonus = true;
        nextShotDamageBonus = damageBonus;
        nextShotAreaBonus = areaBonus;
    }

    void Shoot()
    {
        Vector3 shotDirection = GetShotDirection();
        
        ExecuteShootVisual(shotDirection, true);
        ShootServerRpc(shotDirection);

        nextShotTime = Time.time + (1f / characterData.attackSpeed);
        currentAmmo--;

        if (currentAmmo <= 0) RequestReloadServerRpc();
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 direction)
    {
        ShootVisualClientRpc(direction);
    }

    [ClientRpc]
    private void ShootVisualClientRpc(Vector3 direction)
    {
        if (IsOwner) return;
        ExecuteShootVisual(direction, false);
    }

    private void ExecuteShootVisual(Vector3 direction, bool isOwnerShot)
    {
        if (animator != null) GetComponent<NetworkAnimator>().SetTrigger("Shoot");
        
        PlayShootSound();

        if (firePoint != null)
            firePoint.rotation = Quaternion.LookRotation(direction);

        if (projectilePool != null)
        {
            GameObject visualProjectile = projectilePool.GetProjectile(firePoint.position, Quaternion.LookRotation(direction));
            if (visualProjectile != null)
            {
                ProjectileVisual visualScript = visualProjectile.GetComponent<ProjectileVisual>();
                if (visualScript != null)
                {
                    float damage = 0;
                    bool isCrit = false;
                    
                    if (isOwnerShot)
                        damage = CalculateDamage(out isCrit);
                        
                    visualScript.Initialize(damage, isCrit, characterData.armorPenetration, playerHealth, direction);
                }
            }
        }
    }

    void PlayShootSound()
    {
        string eventToPlay = "";
        bool isFullAuto = characterData.fireMode == FireMode.FullAuto;

        if (tipoDeSom == "Arco")
            eventToPlay = isFullAuto ? eventoTiroContinuoArco : eventoTiroUnicoArco;
        else
            eventToPlay = isFullAuto ? eventoTiroContinuoArma : eventoTiroUnicoArma;

        if (!string.IsNullOrEmpty(eventToPlay))
            RuntimeManager.PlayOneShot(eventToPlay, transform.position);
    }

    float CalculateDamage(out bool isCritical)
    {
        float finalDamage = characterData.damage;
        isCritical = false;

        if (Random.value <= characterData.critChance)
        {
            finalDamage *= characterData.critDamage;
            isCritical = true;
        }

        if (playerHealth != null) finalDamage *= playerHealth.damageMultiplier.Value;

        if (hasNextShotBonus)
        {
            finalDamage *= nextShotDamageBonus;
            hasNextShotBonus = false;
            nextShotDamageBonus = 1f;
            nextShotAreaBonus = 1f;
        }
        return finalDamage;
    }

    public void RequestDamageOnEnemy(ulong enemyNetworkObjectId, float damage, float armorPenetration, bool isCritical)
    {
        if (!IsOwner) return;
        RequestDealDamageServerRpc(enemyNetworkObjectId, damage, armorPenetration, isCritical);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDealDamageServerRpc(ulong enemyNetworkObjectId, float damage, float armorPenetration, bool isCritical)
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject enemyNetObj))
        {
            EnemyHealthSystem enemyHealth = enemyNetObj.GetComponent<EnemyHealthSystem>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, armorPenetration, isCritical);
            }
        }
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

    void StartReloadLocal()
    {
        if (isReloading) return;
        
        float multiplier = 3.0f / characterData.reloadSpeed;
        if (animator != null)
        {
            animator.SetFloat("ReloadSpeedMultiplier", multiplier);
            GetComponent<NetworkAnimator>().SetTrigger("Reload");
        }
        
        if (tipoDeSom == "Arma" && !string.IsNullOrEmpty(eventoRecargaArma))
            RuntimeManager.PlayOneShot(eventoRecargaArma, transform.position);

        isReloading = true;
        reloadStartTime = Time.time;
        
        Invoke("FinishReload", characterData.reloadSpeed);
    }

    void FinishReload()
    {
        currentAmmo = characterData.magazineSize;
        isReloading = false;
    }

    Vector3 GetShotDirection()
    {
        if (mainCamera == null) return transform.forward;
        
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance, hitLayers))
            return (hit.point - firePoint.position).normalized;
        else
            return ray.direction;
    }

    public float GetRemainingReloadTime()
    {
        if (!isReloading) return 0;
        return characterData.reloadSpeed - (Time.time - reloadStartTime);
    }
}