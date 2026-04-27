using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Sync;

public class MergulhoTintaLogic : MonoBehaviour
{
    private float _damage;
    private float _radius;

    private int _originalLayer;
    private string _originalTag;
    private Collider _myCollider;

    private GameObject _puddleInstance;
    private Renderer[] _renderers;
    private PlayerShooting _shootingScript;
    private CommanderAbilityController _abilityScript;
    private PlayerHealthSystem _playerHealth;
    private Ability _sourceAbility;
    private NetworkObject _networkObject;
    private bool _isLocalProxy;

    private const int GHOST_LAYER = 2;
    private const string POCA_TAG = "Poca";

    public LayerMask groundLayerMask = 1;

    private bool HasServerAuthority
    {
        get
        {
            if (_networkObject == null || !_networkObject.IsSpawned)
                return true;

            NetworkManager networkManager = _networkObject.NetworkManager;
            return networkManager == null || networkManager.IsServer;
        }
    }

    private bool IsLocalOwnerInstance
    {
        get
        {
            if (_networkObject == null || !_networkObject.IsSpawned)
                return true;

            NetworkManager networkManager = _networkObject.NetworkManager;
            if (networkManager == null)
                return true;

            return _networkObject.OwnerClientId == networkManager.LocalClientId;
        }
    }

    public bool StartDive(float duration, float damage, float radius, GameObject puddlePrefab, Ability abilitySource, bool validateGround = true)
    {
        _abilityScript = GetComponent<CommanderAbilityController>();
        _sourceAbility = abilitySource;
        _networkObject = GetComponent<NetworkObject>();
        _isLocalProxy = (_networkObject != null && _networkObject.IsSpawned && !HasServerAuthority);

        if (validateGround && !CheckIfGrounded())
        {
            if (_abilityScript != null && HasServerAuthority)
                _abilityScript.SetAbilityUsage(_sourceAbility, false);

            Destroy(this);
            return false;
        }

        if (_abilityScript != null && HasServerAuthority)
            _abilityScript.SetAbilityUsage(_sourceAbility, true);

        _damage = damage;
        _radius = radius;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _shootingScript = GetComponent<PlayerShooting>();
        _playerHealth = GetComponent<PlayerHealthSystem>();

        _myCollider = GetComponent<Collider>();
        if (_myCollider == null) _myCollider = GetComponent<CharacterController>();

        _originalLayer = gameObject.layer;
        _originalTag = gameObject.tag;

        SetLayerRecursively(gameObject, GHOST_LAYER);
        gameObject.tag = POCA_TAG;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1) Physics.IgnoreLayerCollision(GHOST_LAYER, enemyLayer, true);

        if (_shootingScript != null && IsLocalOwnerInstance) _shootingScript.enabled = false;
        if (_abilityScript != null && IsLocalOwnerInstance) _abilityScript.enabled = false;
        foreach (Renderer renderer in _renderers) renderer.enabled = false;

        if (puddlePrefab != null)
        {
            Vector3 spawnPos = GetGroundPosition();
            _puddleInstance = Instantiate(puddlePrefab, spawnPos, Quaternion.Euler(90f, 0f, 0f));
        }

        if (HasServerAuthority)
            ConfundirInimigos(radius * 3f);

        Invoke(nameof(EndDive), duration);
        return true;
    }

    void ConfundirInimigos(float areaDeEfeito)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, areaDeEfeito);
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Enemy"))
                continue;

            NavMeshAgent agent = hit.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            EnemyController enemyController = hit.GetComponent<EnemyController>();
            if (enemyController != null)
                enemyController.LoseTarget();
        }
    }

    Vector3 GetGroundPosition()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, groundLayerMask, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.02f;

        return transform.position + Vector3.up * 0.02f;
    }

    bool CheckIfGrounded()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null && cc.isGrounded) return true;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.6f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.gameObject != gameObject) return true;
        }
        return false;
    }

    void Update()
    {
        if (_puddleInstance != null)
        {
            Vector3 groundPos = GetGroundPosition();
            _puddleInstance.transform.position = new Vector3(transform.position.x, groundPos.y, transform.position.z);
        }
    }

    void EndDive()
    {
        if (_isLocalProxy)
            return;

        if (HasServerAuthority)
        {
            CausarDanoEmArea();
            StartCoroutine(WaitUntilClearToSurface());
            return;
        }

        CompleteOwnerProxySurfaceExit(transform.position);
    }

    IEnumerator WaitUntilClearToSurface()
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
                    Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                    pushDir.y = 0f;

                    NavMeshAgent agent = hit.GetComponent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.Move(pushDir * 3f * Time.deltaTime);
                    }
                    else
                    {
                        Rigidbody rb = hit.GetComponent<Rigidbody>();
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

        if (_abilityScript != null &&
            _networkObject != null &&
            _networkObject.IsSpawned &&
            !IsLocalOwnerInstance)
        {
            _abilityScript.CompleteLocalMergulhoTintaOwnerProxy(surfacePosition);
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
        transform.position = surfacePosition;
        gameObject.tag = _originalTag;
        SetLayerRecursively(gameObject, _originalLayer);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1)
            Physics.IgnoreLayerCollision(GHOST_LAYER, enemyLayer, false);

        if (_shootingScript != null && IsLocalOwnerInstance) _shootingScript.enabled = true;
        if (_abilityScript != null && IsLocalOwnerInstance) _abilityScript.enabled = true;

        foreach (Renderer renderer in _renderers)
            renderer.enabled = true;

        if (_puddleInstance != null)
            Destroy(_puddleInstance);
    }

    void CausarDanoEmArea()
    {
        NetworkGameplayResolver.TryResolveAttackerFromPlayer(gameObject, out ulong attackerClientId, out PlayerHealthSystem attackerHealth);

        Collider[] hits = Physics.OverlapSphere(transform.position, _radius);
        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject)
                continue;

            EnemyHealthSystem enemyHealth = hit.GetComponent<EnemyHealthSystem>();
            if (enemyHealth != null)
            {
                enemyHealth.ApplyAuthoritativeDamage(_damage, 0f, false, attackerClientId, attackerHealth);
            }
        }
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

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }

    void OnDestroy()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1)
            Physics.IgnoreLayerCollision(GHOST_LAYER, enemyLayer, false);
    }
}
