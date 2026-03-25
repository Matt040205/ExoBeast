using UnityEngine;
using UnityEngine.AI;
using System.Collections;

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

    private const int GHOST_LAYER = 2;
    private const string POCA_TAG = "Poca";

    public LayerMask groundLayerMask = 1;

    public void StartDive(float duration, float damage, float radius, GameObject puddlePrefab, Ability abilitySource)
    {
        _abilityScript = GetComponent<CommanderAbilityController>();
        _sourceAbility = abilitySource;

        if (!CheckIfGrounded())
        {
            if (_abilityScript != null)
            {
                _abilityScript.SetAbilityUsage(_sourceAbility, false);
            }
            Destroy(this);
            return;
        }

        if (_abilityScript != null)
        {
            _abilityScript.SetAbilityUsage(_sourceAbility, true);
        }

        _damage = damage;
        _radius = radius;
        _renderers = GetComponentsInChildren<Renderer>();
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

        if (_shootingScript) _shootingScript.enabled = false;
        if (_abilityScript) _abilityScript.enabled = false;
        foreach (var r in _renderers) r.enabled = false;

        if (puddlePrefab != null)
        {
            Vector3 spawnPos = GetGroundPosition();
            _puddleInstance = Instantiate(puddlePrefab, spawnPos, Quaternion.Euler(90f, 0f, 0f));
        }

        ConfundirInimigos(radius * 3f);

        Invoke(nameof(EndDive), duration);
    }

    void ConfundirInimigos(float areaDeEfeito)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, areaDeEfeito);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                NavMeshAgent agent = hit.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.ResetPath();
                    agent.velocity = Vector3.zero;
                }
                hit.SendMessage("LoseTarget", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    Vector3 GetGroundPosition()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.02f;
        }
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
        gameObject.tag = _originalTag;
        if (_shootingScript) _shootingScript.enabled = true;
        if (_abilityScript) _abilityScript.enabled = true;
        foreach (var r in _renderers) r.enabled = true;
        if (_puddleInstance != null) Destroy(_puddleInstance);

        CausarDanoEmArea();
        StartCoroutine(WaitUntilClearToSurface());
    }

    IEnumerator WaitUntilClearToSurface()
    {
        bool isClear = false;
        float maxSafetyWait = 5.0f;
        float timer = 0f;
        LayerMask enemyMask = 1 << LayerMask.NameToLayer("Enemy");

        while (!isClear && timer < maxSafetyWait)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 0.8f, enemyMask);

            if (hits.Length == 0)
            {
                isClear = true;
            }
            else
            {
                foreach (var hit in hits)
                {
                    Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                    pushDir.y = 0;

                    NavMeshAgent agent = hit.GetComponent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.Move(pushDir * 3f * Time.deltaTime);
                    }
                    else
                    {
                        Rigidbody rb = hit.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.linearVelocity = pushDir * 2f;
                        }
                    }
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        SetLayerRecursively(gameObject, _originalLayer);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1) Physics.IgnoreLayerCollision(GHOST_LAYER, enemyLayer, false);

        Destroy(this);
    }

    void CausarDanoEmArea()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _radius);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            EnemyHealthSystem enemyHealth = hit.GetComponent<EnemyHealthSystem>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(_damage, 0f, false);
                if (_playerHealth != null) _playerHealth.TriggerDamageDealt(_damage);
            }
        }
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, newLayer);
    }

    void OnDestroy()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1) Physics.IgnoreLayerCollision(GHOST_LAYER, enemyLayer, false);
    }
}