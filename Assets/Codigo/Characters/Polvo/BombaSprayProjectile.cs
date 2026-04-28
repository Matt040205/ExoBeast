using Unity.Netcode;
using UnityEngine;

public class BombaSprayProjectile : MonoBehaviour
{
    private float radius;
    private float duration;
    private bool isVisualProxy;
    private bool jaBateu;
    private bool jaExplodiu;

    [Header("Configuracoes da Explosao")]
    [Tooltip("Prefab da nuvem de tinta")]
    public GameObject gasCloudPrefab;

    [Header("Configuracoes de Tempo")]
    public float tempoMaximoVida = 7f;
    public float tempoAposImpacto = 3f;

    public void Launch(Vector3 velocity, float newRadius, float cloudDuration)
    {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody != null)
            rigidbody.linearVelocity = velocity;

        radius = newRadius;
        duration = cloudDuration;
        Invoke(nameof(Explode), tempoMaximoVida);
    }

    public void LaunchVisualProxy(Vector3 velocity, float newRadius, float cloudDuration)
    {
        isVisualProxy = true;
        Launch(velocity, newRadius, cloudDuration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (jaBateu || jaExplodiu)
            return;

        jaBateu = true;
        CancelInvoke(nameof(Explode));
        Invoke(nameof(Explode), tempoAposImpacto);
    }

    private void Explode()
    {
        if (jaExplodiu)
            return;

        jaExplodiu = true;

        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (gasCloudPrefab != null && (!isVisualProxy || !isNetworkSession))
            SpawnInkCloud(isNetworkSession);

        Destroy(gameObject);
    }

    private void SpawnInkCloud(bool isNetworkSession)
    {
        GameObject cloud = Instantiate(gasCloudPrefab, transform.position, Quaternion.identity);
        NuvemDeTintaLogic cloudLogic = cloud.GetComponent<NuvemDeTintaLogic>();
        if (cloudLogic != null)
            cloudLogic.Setup(duration, radius);

        if (isNetworkSession && !isVisualProxy && cloud.TryGetComponent(out NetworkObject networkObject))
        {
            networkObject.Spawn();
        }
    }
}
