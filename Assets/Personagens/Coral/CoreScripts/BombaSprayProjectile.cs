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
    [Tooltip("Prefab da nuvem de tinta (Lógica)")]
    public GameObject gasCloudPrefab;

    [Header("VFX")]
    [Tooltip("O prefab que contem o VFX Graph da fumaca")]
    public GameObject smokeVfxPrefab;
    [Tooltip("Tempo que a fumaca visual fica no mapa antes de sumir")]
    public float smokeDuration = 8f;

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
        
        // Lógica de Rede (Nuvem de Tinta) - Apenas o Servidor Spawna a lógica que dá dano/lentidão
        if (gasCloudPrefab != null && (!isVisualProxy || !isNetworkSession))
            SpawnInkCloud(isNetworkSession);

        // Lógica Visual (VFX) - Todos os clientes instanciam a fumaça localmente
        if (smokeVfxPrefab != null)
        {
            GameObject smoke = Instantiate(smokeVfxPrefab, transform.position, Quaternion.identity);
            Destroy(smoke, smokeDuration);
        }

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
