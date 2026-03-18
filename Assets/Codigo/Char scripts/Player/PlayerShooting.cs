using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using System.Collections.Generic;
using Unity.Netcode;

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

    void Start()
    {
        currentAmmo = characterData.magazineSize;
        cameraController = FindObjectOfType<CameraController>();
        mainCamera = Camera.main;
        playerHealth = GetComponent<PlayerHealthSystem>();

        PlayerMovement playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            modelPivot = playerMovement.GetModelPivot();
            if (modelPivot != null)
                animator = modelPivot.GetComponentInChildren<Animator>();
        }

        projectilePool = ProjectilePool.Instance;
        if (projectilePool != null && projectileVisualPrefab != null)
        {
            projectilePool.projectilePrefab = this.projectileVisualPrefab;
            projectilePool.InitializePool();
        }
    }

    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (!this.enabled) return;

        if (ctx.started || ctx.performed) fireInputHeld = true;
        else if (ctx.canceled) fireInputHeld = false;
    }

    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (!this.enabled) return;

        if (ctx.performed && !isReloading && currentAmmo < characterData.magazineSize)
            StartReload();
    }

    void Update()
    {
        if (!IsOwner) return;

        if (PauseControl.isPaused || BuildManager.isBuildingMode) return;

        UpdateAimTargetPosition();

        if (isReloading) return;

        HandleShootingLogic();

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < characterData.magazineSize)
            StartReload();
    }

    void HandleShootingLogic()
    {
        bool canShoot = characterData.fireMode == FireMode.FullAuto ? fireInputHeld : fireInputHeld && Time.time >= nextShotTime;

        if (fireInputHeld && Time.time >= nextShotTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
                if (characterData.fireMode != FireMode.FullAuto) fireInputHeld = false;
            }
            else StartReload();
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
        if (animator != null) animator.SetTrigger("Shoot");
        PlayShootSound();

        if (modelPivot != null)
            firePoint.rotation = Quaternion.LookRotation(GetShotDirection());

        Vector3 shotDirection = GetShotDirection();
        float finalDamage = CalculateDamage(out bool isCritical);

        SpawnProjectile(finalDamage, isCritical, shotDirection);

        nextShotTime = Time.time + (1f / characterData.attackSpeed);
        currentAmmo--;

        if (currentAmmo <= 0) StartReload();
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

        if (playerHealth != null) finalDamage *= playerHealth.damageMultiplier;

        if (hasNextShotBonus)
        {
            finalDamage *= nextShotDamageBonus;
            hasNextShotBonus = false;
            nextShotDamageBonus = 1f;
            nextShotAreaBonus = 1f;
        }
        return finalDamage;
    }

    void SpawnProjectile(float damage, bool isCritical, Vector3 direction)
    {
        if (projectilePool != null)
        {
            GameObject visualProjectile = projectilePool.GetProjectile(firePoint.position, Quaternion.LookRotation(direction));
            if (visualProjectile != null)
            {
                ProjectileVisual visualScript = visualProjectile.GetComponent<ProjectileVisual>();
                if (visualScript != null)
                {
                    visualScript.Initialize(damage, isCritical, characterData.armorPenetration, playerHealth, direction);
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDealDamageServerRpc(ulong enemyNetworkObjectId, float damage, float armorPenetration, bool isCritical)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject enemyNetObj))
        {
            EnemyHealthSystem enemyHealth = enemyNetObj.GetComponent<EnemyHealthSystem>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, armorPenetration, isCritical);
            }
        }
    }

    Vector3 GetShotDirection()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance, hitLayers))
            return (hit.point - firePoint.position).normalized;
        else
            return ray.direction;
    }

    void StartReload()
    {
        if (isReloading) return;
        float multiplier = 3.0f / characterData.reloadSpeed;
        if (animator != null)
        {
            animator.SetFloat("ReloadSpeedMultiplier", multiplier);
            animator.SetTrigger("Reload");
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

    public float GetRemainingReloadTime()
    {
        if (!isReloading) return 0;
        return characterData.reloadSpeed - (Time.time - reloadStartTime);
    }
}