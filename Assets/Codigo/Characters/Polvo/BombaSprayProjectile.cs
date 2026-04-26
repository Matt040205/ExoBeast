using UnityEngine;

public class BombaSprayProjectile : MonoBehaviour
{
    private float _radius;
    private float _duration; // Duracao da nuvem

    [Header("Configuracoes da Explosao")]
    [Tooltip("Prefab da nuvem de gas que aparece quando explode")]
    public GameObject gasCloudPrefab;

    [Header("Configuracoes de Tempo")]
    [Tooltip("Tempo maximo ate explodir sozinho (se nunca bater em nada)")]
    public float tempoMaximoVida = 7f;

    [Tooltip("Tempo para explodir APOS bater na primeira coisa")]
    public float tempoAposImpacto = 3f;

    private bool jaBateu = false;
    private bool jaExplodiu = false;

    // Flag para cópias visuais no owner-cliente: aplicam VFX mas não dano/slow (servidor-only)
    private bool _isVisualProxy = false;

    // Configura os dados quando e lancada (chamado pela Habilidade)
    public void Launch(Vector3 velocity, float radius, float cloudDuration)
    {
        GetComponent<Rigidbody>().linearVelocity = velocity;
        _radius = radius;
        _duration = cloudDuration;

        // 1. Inicia o timer de seguranca (7s)
        Invoke(nameof(Explode), tempoMaximoVida);
    }

    /// <summary>
    /// Versão visual-only para o owner-cliente: a bomba voa e mostra VFX, mas não aplica
    /// slow/cegueira (lógica de gameplay roda apenas no servidor).
    /// </summary>
    public void LaunchVisualProxy(Vector3 velocity, float radius, float cloudDuration)
    {
        _isVisualProxy = true;
        Launch(velocity, radius, cloudDuration);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Se ja bateu ou ja explodiu, ignora batidas subsequentes (quicar no chao)
        if (jaBateu || jaExplodiu) return;

        jaBateu = true;

        // 2. Cancela o timer de 7s, porque agora vale o timer do impacto
        CancelInvoke(nameof(Explode));

        // 3. Inicia o timer de impacto (3s)
        Invoke(nameof(Explode), tempoAposImpacto);
    }

    void Explode()
    {
        // Garante que nao exploda duas vezes
        if (jaExplodiu) return;
        jaExplodiu = true;

        if (gasCloudPrefab != null)
        {
            GameObject cloud = Instantiate(gasCloudPrefab, transform.position, Quaternion.identity);
            cloud.transform.localScale = Vector3.one * _radius;

            // Proxies visuais instanciam a nuvem mas sem lógica de slow/cegueira
            // (lógica de gameplay é servidor-only e roda na instância real)
            if (!_isVisualProxy)
            {
                NuvemDeTintaLogic logic = cloud.GetComponent<NuvemDeTintaLogic>();
                if (logic == null) logic = cloud.AddComponent<NuvemDeTintaLogic>();
                logic.Setup(_duration);
            }
            else
            {
                Destroy(cloud, _duration);
            }
        }

        Destroy(gameObject);
    }
}
