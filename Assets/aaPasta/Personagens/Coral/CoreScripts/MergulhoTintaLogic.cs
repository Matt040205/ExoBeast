using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using ExoBeasts.Multiplayer.Sync;

public class MergulhoTintaLogic : MonoBehaviour
{
    private const int GhostLayer = 2;
    private const string PocaTag = "Poca";

    private static int activeGhostCollisionClaims;

    public LayerMask groundLayerMask = 1;

    private float damage;
    private float radius;

    private string originalTag;
    private readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
    private readonly Dictionary<Renderer, bool> originalRendererStates = new Dictionary<Renderer, bool>();

    private GameObject puddleInstance;
    private PlayerShooting shootingScript;
    private PlayerCombatManager combatScript;
    private CommanderAbilityController abilityScript;
    private Ability sourceAbility;
    private NetworkObject networkObject;
    private bool isLocalProxy;
    private bool stateApplied;
    private bool stateRestored;

    private bool shootingWasEnabled;
    private bool combatWasEnabled;

    private Material _diveShaderMaterial;
    private float _dissolveDuration;
    private Sprite _fallbackPuddleSprite;
    private float _fallbackPuddleWorldSize = 4.5f;
    private readonly Dictionary<SkinnedMeshRenderer, Material[]> originalSkinnedMaterials = new Dictionary<SkinnedMeshRenderer, Material[]>();
    private readonly List<Material> dissolveInstances = new List<Material>();
    private Coroutine dissolveCoroutine;

    private bool HasServerAuthority
    {
        get
        {
            if (networkObject == null || !networkObject.IsSpawned)
                return true;

            NetworkManager networkManager = networkObject.NetworkManager;
            return networkManager == null || networkManager.IsServer;
        }
    }

    private bool IsLocalOwnerInstance
    {
        get
        {
            if (networkObject == null || !networkObject.IsSpawned)
                return true;

            NetworkManager networkManager = networkObject.NetworkManager;
            if (networkManager == null)
                return true;

            return networkObject.OwnerClientId == networkManager.LocalClientId;
        }
    }

    public bool StartDive(
        float duration,
        float damage,
        float radius,
        GameObject puddlePrefab,
        Ability abilitySource,
        bool validateGround = true)
    {
        abilityScript = GetComponent<CommanderAbilityController>();
        sourceAbility = abilitySource;
        networkObject = GetComponent<NetworkObject>();

        if (abilitySource is HabilidadeMergulhoTinta mta)
        {
            _diveShaderMaterial = mta.diveShaderMaterial;
            _dissolveDuration = Mathf.Max(0.1f, mta.dissolveDuration);
            _fallbackPuddleSprite = mta.fallbackPuddleSprite;
            _fallbackPuddleWorldSize = Mathf.Max(0.1f, mta.fallbackPuddleWorldSize);
        }
        isLocalProxy = networkObject != null && networkObject.IsSpawned && !HasServerAuthority;

        if (validateGround && !CanStartDiveFromGround())
        {
            if (abilityScript != null && HasServerAuthority)
                abilityScript.SetAbilityUsage(sourceAbility, false);

            Destroy(this);
            return false;
        }

        if (abilityScript != null && HasServerAuthority)
            abilityScript.DeferAbilityCooldownUntilReleased(sourceAbility);

        this.damage = damage;
        this.radius = radius;

        CacheDependencies();
        ApplyDiveState(puddlePrefab);

        if (HasServerAuthority)
            ConfundirInimigos(radius * 3f);

        Invoke(nameof(EndDive), duration);
        return true;
    }

    private void CacheDependencies()
    {
        shootingScript = GetComponent<PlayerShooting>();
        combatScript = GetComponent<PlayerCombatManager>();
    }

    private void ApplyDiveState(GameObject puddlePrefab)
    {
        if (stateApplied)
            return;

        originalTag = gameObject.tag;
        originalLayers.Clear();
        foreach (Transform target in GetComponentsInChildren<Transform>(true))
            originalLayers[target] = target.gameObject.layer;

        originalRendererStates.Clear();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            originalRendererStates[renderer] = renderer.enabled;

        SetLayerRecursively(gameObject, GhostLayer);
        gameObject.tag = PocaTag;
        SetGhostEnemyCollisionIgnored(true);

        if (IsLocalOwnerInstance)
        {
            shootingWasEnabled = shootingScript != null && shootingScript.enabled;
            combatWasEnabled = combatScript != null && combatScript.enabled;

            if (shootingWasEnabled)
                shootingScript.enabled = false;

            if (combatWasEnabled)
                combatScript.enabled = false;
        }

        if (IsLocalOwnerInstance && _diveShaderMaterial != null)
        {
            SwapToDissolveShader();
            dissolveCoroutine = StartCoroutine(DissolveIn(puddlePrefab));
        }
        else
        {
            foreach (Renderer renderer in originalRendererStates.Keys)
            {
                if (renderer != null)
                    renderer.enabled = false;
            }

            SpawnPuddleVisual(puddlePrefab);
        }

        stateApplied = true;
        stateRestored = false;
    }

    private static void SetGhostEnemyCollisionIgnored(bool shouldIgnore)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer == -1)
            return;

        if (shouldIgnore)
        {
            activeGhostCollisionClaims++;
            Physics.IgnoreLayerCollision(GhostLayer, enemyLayer, true);
            return;
        }

        activeGhostCollisionClaims = Mathf.Max(0, activeGhostCollisionClaims - 1);
        if (activeGhostCollisionClaims == 0)
            Physics.IgnoreLayerCollision(GhostLayer, enemyLayer, false);
    }

    private void ConfundirInimigos(float areaDeEfeito)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, areaDeEfeito);
        foreach (Collider hit in hits)
        {
            EnemyController enemyController = hit.GetComponentInParent<EnemyController>();
            if (enemyController == null)
                continue;

            NavMeshAgent agent = enemyController.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            enemyController.LoseTarget();
        }
    }

    private Vector3 GetGroundPosition()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
        
        Vector3 bestPoint = transform.position;
        float highestY = -9999f;
        bool found = false;

        foreach (var hit in hits)
        {
            if (hit.collider.transform.root != transform.root && !hit.collider.isTrigger)
            {
                if (hit.point.y > highestY)
                {
                    highestY = hit.point.y;
                    bestPoint = hit.point;
                    found = true;
                }
            }
        }

        if (found) return bestPoint;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
            return new Vector3(transform.position.x, cc.bounds.min.y, transform.position.z);

        return transform.position;
    }

    private bool CheckIfGrounded()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null && movement.IsGroundedForGameplay(0.75f))
            return true;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null && cc.enabled && cc.isGrounded)
            return true;

        Vector3 origin = transform.position + Vector3.up * 0.2f;
        float probeRadius = cc != null ? Mathf.Max(0.15f, cc.radius * 0.9f) : 0.2f;
        float probeDistance = cc != null ? Mathf.Max(0.75f, cc.skinWidth + 0.5f) : 0.85f;

        if (Physics.SphereCast(origin, probeRadius, Vector3.down, out RaycastHit hit, probeDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            return hit.collider.gameObject != gameObject;

        return false;
    }

    private bool CanStartDiveFromGround()
    {
        // OPTIMIZATION BUGFIX (Sprint 3 / G3.2 - 2026-05-08): o servidor nao
        // recebe mais netIsGrounded continuo. Para cliente nao-host, confiar no
        // grounded enviado junto do RPC de habilidade apenas durante esta ativacao.
        if (IsServerValidatingRemoteOwner() &&
            abilityScript != null &&
            abilityScript.TryGetOwnerGroundedForAbilityRequest(out bool ownerGrounded) &&
            ownerGrounded)
        {
            return true;
        }

        return CheckIfGrounded();
    }

    private bool IsServerValidatingRemoteOwner()
    {
        return HasServerAuthority &&
               networkObject != null &&
               networkObject.IsSpawned &&
               networkObject.OwnerClientId != NetworkManager.ServerClientId;
    }

    private PlayerMovement _cachedPlayerMovement;

    private void Update()
    {
        if (puddleInstance == null)
            return;

        Vector3 groundPos = GetGroundPosition();
        puddleInstance.transform.position = new Vector3(transform.position.x, groundPos.y, transform.position.z);

        if (_cachedPlayerMovement == null)
            _cachedPlayerMovement = GetComponent<PlayerMovement>();

        if (_cachedPlayerMovement != null && _cachedPlayerMovement.GetModelPivot() != null)
        {
            puddleInstance.transform.rotation = _cachedPlayerMovement.GetModelPivot().rotation;
        }
    }

    private void EndDive()
    {
        if (isLocalProxy)
            return;

        if (HasServerAuthority)
        {
            ApplyExitImpact();
            StartCoroutine(WaitUntilClearToSurface());
            return;
        }

        CompleteOwnerProxySurfaceExit(transform.position);
    }

    private void ApplyExitImpact()
    {
        NetworkGameplayResolver.TryResolveAttackerFromPlayer(gameObject, out ulong attackerClientId, out PlayerHealthSystem attackerHealth);

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        HashSet<EnemyHealthSystem> damagedEnemies = new HashSet<EnemyHealthSystem>();
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
                continue;

            EnemyHealthSystem enemyHealth = hit.GetComponentInParent<EnemyHealthSystem>();
            if (enemyHealth == null || !damagedEnemies.Add(enemyHealth))
                continue;

            enemyHealth.ApplyAuthoritativeDamage(damage, 0f, false, attackerClientId, attackerHealth);

        }
    }

    private IEnumerator WaitUntilClearToSurface()
    {
        bool isClear = false;
        float maxSafetyWait = 5.0f;
        float timer = 0f;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        LayerMask enemyMask = enemyLayer >= 0 ? (1 << enemyLayer) : 0;

        while (!isClear && timer < maxSafetyWait)
        {
            Collider[] hits = enemyMask != 0
                ? Physics.OverlapSphere(transform.position, 0.8f, enemyMask)
                : new Collider[0];

            if (hits.Length == 0)
            {
                isClear = true;
            }
            else
            {
                foreach (Collider hit in hits)
                {
                    if (hit == null)
                        continue;

                    Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                    pushDir.y = 0f;

                    NavMeshAgent agent = hit.GetComponentInParent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.Move(pushDir * 3f * Time.deltaTime);
                    }
                    else
                    {
                        Rigidbody rb = hit.GetComponentInParent<Rigidbody>();
                        if (rb != null)
                            rb.linearVelocity = pushDir * 2f;
                    }
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        FinalizeServerSurfaceExit(transform.position);
    }

    private void FinalizeServerSurfaceExit(Vector3 surfacePosition)
    {
        RestoreLocalPresentation(surfacePosition);
        RefreshEnemyTargets();
        abilityScript?.StartAbilityCooldown(sourceAbility);

        if (abilityScript != null &&
            networkObject != null &&
            networkObject.IsSpawned &&
            !IsLocalOwnerInstance)
        {
            abilityScript.CompleteLocalMergulhoTintaOwnerProxy(surfacePosition);
        }

        Destroy(this);
    }

    public void CompleteOwnerProxySurfaceExit(Vector3 surfacePosition)
    {
        RestoreLocalPresentation(surfacePosition);
        Destroy(this);
    }

    private void RestoreLocalPresentation(Vector3 surfacePosition)
    {
        if (!stateApplied || stateRestored)
            return;

        transform.position = surfacePosition;
        gameObject.tag = originalTag;

        foreach (KeyValuePair<Transform, int> entry in originalLayers)
        {
            if (entry.Key != null)
                entry.Key.gameObject.layer = entry.Value;
        }

        SetGhostEnemyCollisionIgnored(false);

        if (IsLocalOwnerInstance)
        {
            if (shootingScript != null)
                shootingScript.enabled = shootingWasEnabled;

            if (combatScript != null)
                combatScript.enabled = combatWasEnabled;
        }

        if (IsLocalOwnerInstance && _diveShaderMaterial != null)
        {
            if (dissolveCoroutine != null)
            {
                StopCoroutine(dissolveCoroutine);
                dissolveCoroutine = null;
                RestoreOriginalSkinnedMaterials();
            }

            foreach (KeyValuePair<Renderer, bool> entry in originalRendererStates)
                if (entry.Key != null) entry.Key.enabled = entry.Value;

            SwapToDissolveShader();
            SetDissolveAmount(1f);
            dissolveCoroutine = StartCoroutine(DissolveOut());
        }
        else
        {
            foreach (KeyValuePair<Renderer, bool> entry in originalRendererStates)
            {
                if (entry.Key != null)
                    entry.Key.enabled = entry.Value;
            }
        }

        if (puddleInstance != null)
            Destroy(puddleInstance);

        puddleInstance = null;
        stateRestored = true;
        stateApplied = false;
    }

    private void RefreshEnemyTargets()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (EnemyController enemy in enemies)
        {
            if (enemy != null)
                enemy.RefreshTargetNow();
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }

    private void SwapToDissolveShader()
    {
        foreach (Material m in dissolveInstances)
            if (m != null) Destroy(m);
        dissolveInstances.Clear();
        originalSkinnedMaterials.Clear();

        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            originalSkinnedMaterials[smr] = smr.sharedMaterials;
            Material[] newMats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                Material inst = new Material(_diveShaderMaterial);
                dissolveInstances.Add(inst);
                newMats[i] = inst;
            }
            smr.materials = newMats;
        }
    }

    private void RestoreOriginalSkinnedMaterials()
    {
        foreach (KeyValuePair<SkinnedMeshRenderer, Material[]> pair in originalSkinnedMaterials)
        {
            if (pair.Key != null)
                pair.Key.materials = pair.Value;
        }
        foreach (Material m in dissolveInstances)
            if (m != null) Destroy(m);
        dissolveInstances.Clear();
        originalSkinnedMaterials.Clear();
    }

    private void SetDissolveAmount(float value)
    {
        foreach (Material m in dissolveInstances)
            if (m != null) m.SetFloat("_dissolveamount", value);
    }

    private IEnumerator DissolveIn(GameObject puddlePrefab)
    {
        float elapsed = 0f;
        while (elapsed < _dissolveDuration)
        {
            elapsed += Time.deltaTime;
            SetDissolveAmount(Mathf.Clamp01(elapsed / _dissolveDuration));
            yield return null;
        }

        SetDissolveAmount(1f);

        foreach (Renderer renderer in originalRendererStates.Keys)
            if (renderer != null) renderer.enabled = false;

        RestoreOriginalSkinnedMaterials();

        if (puddlePrefab != null)
            SpawnPuddleVisual(puddlePrefab);

        dissolveCoroutine = null;
    }

    private void SpawnPuddleVisual(GameObject puddlePrefab)
    {
        if (puddlePrefab != null)
        {
            Vector3 spawnPos = GetGroundPosition();
            
            // Pega a rotação do modelPivot (que é o que realmente gira na personagem)
            Quaternion spawnRot = transform.rotation;
            PlayerMovement pm = GetComponent<PlayerMovement>();
            if (pm != null && pm.GetModelPivot() != null)
            {
                spawnRot = pm.GetModelPivot().rotation;
            }

            puddleInstance = Instantiate(puddlePrefab, spawnPos, spawnRot);
            
            CaminhoInkController inkController = puddleInstance.GetComponent<CaminhoInkController>();
            if (inkController != null)
                inkController.SetSphereActive(true); // Habilidade tem a esfera ativada
            
            return;
        }

        if (_fallbackPuddleSprite != null)
        {
            puddleInstance = CreateSimplePuddleSprite(_fallbackPuddleSprite, _fallbackPuddleWorldSize);
        }
    }

    private GameObject CreateSimplePuddleSprite(Sprite sprite, float worldSize)
    {
        if (sprite == null)
            return null;

        GameObject puddleVisual = new GameObject("PocaSpriteVisual");
        puddleVisual.transform.SetPositionAndRotation(GetGroundPosition(), Quaternion.Euler(90f, 0f, 0f));

        SpriteRenderer spriteRenderer = puddleVisual.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
        spriteRenderer.sortingOrder = 0;

        float spriteWidth = sprite.bounds.size.x;
        float spriteHeight = sprite.bounds.size.y;
        float largestDimension = Mathf.Max(spriteWidth, spriteHeight, 0.01f);
        float scaleFactor = Mathf.Max(0.1f, worldSize) / largestDimension;
        puddleVisual.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

        return puddleVisual;
    }

    private IEnumerator DissolveOut()
    {
        float elapsed = 0f;
        while (elapsed < _dissolveDuration)
        {
            elapsed += Time.deltaTime;
            SetDissolveAmount(1f - Mathf.Clamp01(elapsed / _dissolveDuration));
            yield return null;
        }

        SetDissolveAmount(0f);
        RestoreOriginalSkinnedMaterials();
        dissolveCoroutine = null;
    }

    private void OnDestroy()
    {
        CancelInvoke();
        StopAllCoroutines();
        RestoreOriginalSkinnedMaterials();
        RestoreLocalPresentation(transform.position);
    }
}
