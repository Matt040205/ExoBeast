using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using ExoBeasts.Multiplayer.GameServer; // Necessário para achar o PlayerRegistry

/// <summary>
/// ── PlayerShooting ─────────────────────────────────────
/// Sistema de tiro a distancia com projeteis visuais locais.
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
    public GameObject muzzleFlashPrefab;

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
    public int maxAmmo;
    public bool isReloading;
    public bool isFiring;
    public float reloadStartTime;

    private float nextShotTime;
    private CameraController cameraController;
    private Transform modelPivot;
    private ProjectilePool projectilePool;
    private Camera mainCamera;
    private Animator animator;
    private NetworkAnimator networkAnimator;

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

        // Garante que a HUD seja avisada imediatamente assim que a arma liga!
        if (PlayerHUD.Instance != null && playerHealth != null)
        {
            PlayerHUD.Instance.RegistrarJogador(playerHealth);
        }
    }

    private void InitializeShooting()
    {
        // =================================================================
        // INJEÇÃO LOCAL: Como o ScriptableObject não viaja pela rede sozinho,
        // o próprio Cliente busca os dados locais dele caso estejam nulos!
        // =================================================================
        if (characterData == null)
        {
            if (PlayerRegistry.Instance != null && GameDataManager.Instance != null)
            {
                int charIndex = PlayerRegistry.Instance.GetPlayerCharacterChoice(OwnerClientId);
                if (charIndex >= 0 && charIndex < GameDataManager.Instance.bibliotecaOriginalPersonagens.Count)
                {
                    characterData = GameDataManager.Instance.bibliotecaOriginalPersonagens[charIndex];
                }
            }

            // Fallback caso seja Singleplayer ou o Registry demore
            if (characterData == null && GameDataManager.Instance != null && GameDataManager.Instance.equipeSelecionada[0] != null)
            {
                characterData = GameDataManager.Instance.equipeSelecionada[0];
            }
        }

        maxAmmo = (characterData != null) ? characterData.magazineSize : 10;
        currentAmmo = maxAmmo;

        mainCamera = Camera.main;
        playerHealth = GetComponent<PlayerHealthSystem>();

        PlayerMovement playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            modelPivot = playerMovement.GetModelPivot();
            if (modelPivot != null)
                animator = modelPivot.GetComponentInChildren<Animator>();
        }

        networkAnimator = GetComponent<NetworkAnimator>();
        if (networkAnimator == null) networkAnimator = GetComponentInChildren<NetworkAnimator>();

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

        if (ctx.performed && !isReloading && currentAmmo < maxAmmo)
            RequestReloadServerRpc();
    }

    void Update()
    {
        if (!IsOwner) return;
        if (PauseControl.isPaused) return;

        if (PauseControl.isPaused || BuildManager.isBuildingMode) return;

        UpdateAimTargetPosition();

        if (isReloading) return;

        HandleShootingLogic();

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo)
            RequestReloadServerRpc();
    }

    void HandleShootingLogic()
    {
        if (fireInputHeld && Time.time >= nextShotTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
                if (characterData != null && characterData.fireMode != FireMode.FullAuto) fireInputHeld = false;
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

        float atkSpeed = (characterData != null && characterData.attackSpeed > 0) ? characterData.attackSpeed : 1f;
        nextShotTime = Time.time + (1f / atkSpeed);
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
        // Trigger apenas pelo owner: NGO NetworkAnimator propaga automaticamente para os remotos.
        // Remotos receberiam o trigger via ClientRpc E via propagação do NetworkAnimator — duplicata.
        if (isOwnerShot && networkAnimator != null) networkAnimator.SetTrigger("Shoot");

        PlayShootSound();

        if (firePoint != null)
        {
            firePoint.rotation = Quaternion.LookRotation(direction);

            if (muzzleFlashPrefab != null)
            {
                GameObject flash = GlobalVFXPool.GetVFX(muzzleFlashPrefab, firePoint.position, firePoint.rotation, 1.5f);
                flash.transform.SetParent(firePoint);
            }
        }

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

                    float armPen = (characterData != null) ? characterData.armorPenetration : 0f;
                    
                    bool isEmpoweredBySkill = hasNextShotBonus;

                    if (isOwnerShot)
                        damage = CalculateDamage(out isCrit);

                    visualScript.Initialize(damage, isCrit, armPen, playerHealth, direction, isEmpoweredBySkill);
                }
            }
        }
    }

    void PlayShootSound()
    {
        string eventToPlay = "";
        bool isFullAuto = (characterData != null && characterData.fireMode == FireMode.FullAuto);

        if (tipoDeSom == "Arco")
            eventToPlay = isFullAuto ? eventoTiroContinuoArco : eventoTiroUnicoArco;
        else
            eventToPlay = isFullAuto ? eventoTiroContinuoArma : eventoTiroUnicoArma;

        if (!string.IsNullOrEmpty(eventToPlay))
            RuntimeManager.PlayOneShot(eventToPlay, transform.position);
    }

    float CalculateDamage(out bool isCritical)
    {
        float finalDamage = (characterData != null) ? characterData.damage : 10f;
        isCritical = false;

        if (characterData != null && Random.value <= characterData.critChance)
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
        
        // Passo 2: Em vez de disparar um ServerRpc desta arma, chamamos diretamente 
        // o ponto centralizado de dano que fica no próprio inimigo.
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject enemyNetObj))
        {
            var networkedEnemy = enemyNetObj.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedEnemy>();
            if (networkedEnemy != null && networkedEnemy.IsSpawned)
            {
                // Como chamamos a função do inimigo a partir deste script local, o NGO 
                // usa nosso SenderClientId embutido automaticamente nos pacotes de rede.
                networkedEnemy.TakeDamageServerRpc(damage, armorPenetration, isCritical);
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

        float relSpeed = (characterData != null && characterData.reloadSpeed > 0) ? characterData.reloadSpeed : 2f;
        float multiplier = 3.0f / relSpeed;

        if (animator != null && networkAnimator != null)
        {
            animator.SetFloat("ReloadSpeedMultiplier", multiplier);
            networkAnimator.SetTrigger("Reload");
        }

        if (tipoDeSom == "Arma" && !string.IsNullOrEmpty(eventoRecargaArma))
            RuntimeManager.PlayOneShot(eventoRecargaArma, transform.position);

        isReloading = true;
        reloadStartTime = Time.time;

        Invoke("FinishReload", relSpeed);
    }

    void FinishReload()
    {
        currentAmmo = maxAmmo;
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
        float relSpeed = (characterData != null) ? characterData.reloadSpeed : 2f;
        return relSpeed - (Time.time - reloadStartTime);
    }
}