using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using ExoBeasts.Multiplayer.GameServer; // Necessário para achar o PlayerRegistry
using ExoBeasts.Multiplayer.Lobby;     // LobbyManager — para índice de membro do lobby
using ExoBeasts.Multiplayer.Auth;      // SessionManager — para productUserId local

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

    [Header("Explosão em Área (Coruja)")]
    public GameObject explosionVfxPrefab;
    public string explosionVfxRadiusParam = "Radius";

    [Header("Juice Configs")]
    [SerializeField] private CameraShakeConfig empoweredShotShake = new CameraShakeConfig(3f, 0.5f, 0.3f);

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
            // Caminho primário (funciona para o Host, que é também o servidor):
            // PlayerRegistry.playerCharacterChoices só é populado no servidor,
            // por isso esse path retorna 0 (default) para clientes não-host.
            if (IsServer && PlayerRegistry.Instance != null && GameDataManager.Instance != null)
            {
                int charIndex = PlayerRegistry.Instance.GetPlayerCharacterChoice(OwnerClientId);
                if (charIndex >= 0 && charIndex < GameDataManager.Instance.bibliotecaOriginalPersonagens.Count)
                {
                    characterData = GameDataManager.Instance.bibliotecaOriginalPersonagens[charIndex];
                    Debug.Log($"[PlayerShooting] characterData resolvido via PlayerRegistry: {characterData?.name} (charIndex={charIndex})");
                }
            }

            // Fallback multiplayer/singleplayer: resolve pelo índice de membro no lobby.
            // Necessário para clientes não-host, pois o Registry não replica entre peers.
            // Slot layout: 2p → P0=[0-3], P1=[4-7] | 3p → P0=[0-3], P1=[4-5], P2=[6-7] | 4p → Px=[x*2, x*2+1]
            if (characterData == null && GameDataManager.Instance != null)
            {
                characterData = ResolveLocalCommanderCharacter();
                Debug.Log($"[PlayerShooting] characterData resolvido via lobby index: {characterData?.name}");
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
        if (projectilePool == null)
        {
            Debug.LogWarning("[PlayerShooting] ProjectilePool.Instance é nulo! Adicione um ProjectilePool à cena. Projéteis visuais não serão spawnados.");
        }
        else if (projectileVisualPrefab == null)
        {
            Debug.LogWarning("[PlayerShooting] projectileVisualPrefab é nulo no Inspector. Projéteis visuais não serão spawnados.");
        }
        else
        {
            projectilePool.projectilePrefab = this.projectileVisualPrefab;
            projectilePool.InitializePool();
        }
    }

    /// <summary>
    /// Resolve o CharacterBase do Comandante deste jogador local consultando o índice
    /// de membro no lobby. Necessário para clientes não-host porque o PlayerRegistry
    /// (server-only) não replica os dados entre peers via NGO.
    ///
    /// Layout de slots (equipeSelecionada):
    ///  2 jogadores → P0=[0-3], P1=[4-7]          (commander = slot inicial)
    ///  3 jogadores → P0=[0-3], P1=[4-5], P2=[6-7]
    ///  4 jogadores → P0=[0-1], P1=[2-3], P2=[4-5], P3=[6-7]
    ///
    /// Fallback seguro: equipe[0] (Singleplayer).
    /// </summary>
    private CharacterBase ResolveLocalCommanderCharacter()
    {
        var gdm = GameDataManager.Instance;
        if (gdm == null) return null;

        var equipe = gdm.equipeSelecionada;
        if (equipe == null || equipe.Length == 0) return null;

        int commanderSlot = 0; // Padrão: slot 0 (Singleplayer / P0)

        var lobbyMgr = LobbyManager.Instance;
        var sessionMgr = SessionManager.Instance;

        if (lobbyMgr != null && sessionMgr != null)
        {
            var membros = lobbyMgr.GetMembers();
            string meuId = sessionMgr.GetUserId();
            int meuIndice = membros.FindIndex(m => m.productUserId == meuId);
            int total = membros.Count;

            if (meuIndice >= 0)
            {
                if      (total == 2) commanderSlot = meuIndice * 4;
                else if (total == 3) commanderSlot = meuIndice == 0 ? 0 : meuIndice == 1 ? 4 : 6;
                else if (total == 4) commanderSlot = meuIndice * 2;
                // total == 1 → commanderSlot permanece 0 (Singleplayer)
            }

            Debug.Log($"[PlayerShooting] Lobby index={meuIndice}/{total} → commanderSlot={commanderSlot}");
        }
        else
        {
            Debug.Log("[PlayerShooting] LobbyManager ou SessionManager nulo — usando slot 0 (Singleplayer).");
        }

        if (commanderSlot >= 0 && commanderSlot < equipe.Length && equipe[commanderSlot] != null)
            return equipe[commanderSlot];

        // Último recurso: slot 0
        return equipe.Length > 0 ? equipe[0] : null;
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
                    float explosionRadius = 0f;

                    if (isOwnerShot)
                        damage = CalculateDamage(out isCrit, out explosionRadius);

                    visualScript.Initialize(damage, isCrit, armPen, playerHealth, direction, isEmpoweredBySkill, explosionRadius);

                    if (isOwnerShot && tipoDeSom == "Arco" && isEmpoweredBySkill)
                    {
                        JuiceEvents.OnCameraShake?.Invoke(-direction, empoweredShotShake.amplitude, empoweredShotShake.frequency, empoweredShotShake.duration);
                    }
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

    float CalculateDamage(out bool isCritical, out float areaRadius)
    {
        float finalDamage = (characterData != null) ? characterData.damage : 10f;
        isCritical = false;
        areaRadius = 0f;

        if (characterData != null && Random.value <= characterData.critChance)
        {
            finalDamage *= characterData.critDamage;
            isCritical = true;
        }

        if (playerHealth != null) finalDamage *= playerHealth.damageMultiplier.Value;

        if (hasNextShotBonus)
        {
            finalDamage *= nextShotDamageBonus;
            areaRadius = nextShotAreaBonus;
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

    public void RequestExplosionVfx(Vector3 position, float radius)
    {
        if (IsOwner)
        {
            PlayExplosionLocal(position, radius);
            RequestExplosionVfxServerRpc(position, radius);
        }
    }

    [ServerRpc]
    private void RequestExplosionVfxServerRpc(Vector3 position, float radius)
    {
        PlayExplosionVfxClientRpc(position, radius);
    }

    [ClientRpc]
    private void PlayExplosionVfxClientRpc(Vector3 position, float radius)
    {
        if (IsOwner) return;
        PlayExplosionLocal(position, radius);
    }

    private void PlayExplosionLocal(Vector3 position, float radius)
    {
        if (explosionVfxPrefab != null)
        {
            GameObject vfx = GlobalVFXPool.GetVFX(explosionVfxPrefab, position, Quaternion.identity, 3f);
            var vfxGraph = vfx.GetComponent<UnityEngine.VFX.VisualEffect>();
            if (vfxGraph != null && vfxGraph.HasFloat(explosionVfxRadiusParam))
            {
                vfxGraph.SetFloat(explosionVfxRadiusParam, radius);
            }
            else
            {
                vfx.transform.localScale = Vector3.one * radius;
            }
        }
    }
}