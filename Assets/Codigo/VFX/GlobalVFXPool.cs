using UnityEngine;
using UnityEngine.Pool;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ── GlobalVFXPool ──────────────────────────────────────────
/// Arquitetura moderna de Object Pooling para Efeitos Visuais
/// e Partículas, utilizando a API nativa do Unity 2021+
/// (UnityEngine.Pool).
///
///  ▸ Alta performance, sem lixo de memória na alocação.
///  ▸ Centralizado em "nome do Prefab".
///  ▸ Retorno automático via OnParticleSystemStopped ou timer.
/// ───────────────────────────────────────────────────────────
/// </summary>
public class GlobalVFXPool : MonoBehaviour
{
    public static GlobalVFXPool Instance { get; private set; }

    private Dictionary<string, ObjectPool<GameObject>> _pools = new Dictionary<string, ObjectPool<GameObject>>();
    private Dictionary<string, GameObject> _prefabMap = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Instancia ou busca um VFX do pool. Opcional: fornece um autoReleaseDelay para
    /// desligá-lo automaticamente apóx X segundos.
    /// Caso delay seja nulo, tentará usar o callback OnParticleSystemStopped (Stop Action = Callback).
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, float? autoReleaseDelay = null)
    {
        string key = prefab.name;

        if (!_pools.ContainsKey(key))
        {
            _prefabMap[key] = prefab;
            _pools[key] = new ObjectPool<GameObject>(
                createFunc: () => {
                    GameObject go = Instantiate(_prefabMap[key], transform);
                    go.name = key;
                    return go;
                },
                actionOnGet: (go) => {
                    go.transform.position = position;
                    go.transform.rotation = rotation;
                    go.SetActive(true);
                    
                    // Varredura à prova de balas para Particle Systems (Pai e Filhos)
                    var particleSystems = go.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in particleSystems)
                    {
                        ps.Play(true);
                    }
                    
                    // Varredura para Visual Effect Graph (Pai e Filhos)
                    var vfxGraphs = go.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
                    foreach (var vfx in vfxGraphs)
                    {
                        vfx.Play();
                    }
                },
                actionOnRelease: (go) => {
                    go.SetActive(false);
                    go.transform.SetParent(transform);
                },
                actionOnDestroy: (go) => {
                    Destroy(go);
                },
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 100
            );
        }

        GameObject instance = _pools[key].Get();

        // Acopla o ajudante de retorno automático (Coroutine ou Callback nativo).
        PooledVFXReturner returner = instance.GetComponent<PooledVFXReturner>();
        if (returner == null) returner = instance.AddComponent<PooledVFXReturner>();
        
        returner.Setup(key, autoReleaseDelay);

        return instance;
    }

    /// <summary>
    /// Força o retorno manual de um VFX ao pool.
    /// </summary>
    public void Release(string prefabName, GameObject instance)
    {
        if (_pools.TryGetValue(prefabName, out var pool))
        {
            if (instance.activeSelf)
                pool.Release(instance);
        }
    }

    /// <summary>
    /// Wrapper estático para facilidade de uso global sem chamar a Instance manualmente.
    /// Exemplo: GlobalVFXPool.GetVFX(muzzleFlashPrefab, pos, rot, 1.5f);
    /// </summary>
    public static GameObject GetVFX(GameObject prefab, Vector3 position, Quaternion rotation, float? autoReleaseDelay = null)
    {
        if (Instance != null)
        {
            return Instance.Get(prefab, position, rotation, autoReleaseDelay);
        }
        else
        {
            // Fallback caso o Singleton não esteja na cena ainda
            GameObject fallback = Instantiate(prefab, position, rotation);
            if (autoReleaseDelay.HasValue) Destroy(fallback, autoReleaseDelay.Value);
            return fallback;
        }
    }

    public static void ReleaseVFX(GameObject prefab, GameObject instance)
    {
        if (Instance != null)
        {
            Instance.Release(prefab.name, instance);
        }
        else
        {
            Destroy(instance);
        }
    }
}

/// <summary>
/// Ajudante de Componente acoplado à instância instanciada,
/// ouvindo OnParticleSystemStopped() ou rodando uma Coroutine
/// paralela para garantir que o GameObject vá pra .Release()
/// quando terminar.
/// </summary>
public class PooledVFXReturner : MonoBehaviour
{
    private string _prefabName;
    private Coroutine _timerCoroutine;

    public void Setup(string prefabName, float? delaySeconds)
    {
        _prefabName = prefabName;
        
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);

        if (delaySeconds.HasValue)
        {
            _timerCoroutine = StartCoroutine(ReleaseTimer(delaySeconds.Value));
        }
        else
        {
            // Se for um sistema de partículas, prepara o Stop Action para invocar OnParticleSystemStopped
            var ps = GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Callback;
            }
        }
    }

    private IEnumerator ReleaseTimer(float time)
    {
        yield return new WaitForSeconds(time);
        if (gameObject.activeSelf && GlobalVFXPool.Instance != null)
        {
            GlobalVFXPool.Instance.Release(_prefabName, gameObject);
        }
    }

    // Callback nativo do Unity acionado quando Stop Action = Callback
    private void OnParticleSystemStopped()
    {
        if (gameObject.activeSelf && GlobalVFXPool.Instance != null)
        {
            GlobalVFXPool.Instance.Release(_prefabName, gameObject);
        }
    }
}
