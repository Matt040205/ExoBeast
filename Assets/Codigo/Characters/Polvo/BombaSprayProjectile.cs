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

    // Configura os dados quando e lancada (chamado pela Habilidade)
    public void Launch(Vector3 velocity, float radius, float cloudDuration)
    {
        GetComponent<Rigidbody>().linearVelocity = velocity;
        _radius = radius;
        _duration = cloudDuration;

        // 1. Inicia o timer de seguranca (7s)
        Invoke(nameof(Explode), tempoMaximoVida);
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

        // Cria a nuvem de gas no local da granada
        if (gasCloudPrefab != null)
        {
            Quaternion rot = Quaternion.identity;

            // Instancia a nuvem
            GameObject cloud = Instantiate(gasCloudPrefab, transform.position, rot);

            // Configura o tamanho
            cloud.transform.localScale = Vector3.one * _radius;

            // Inicializa a logica de dano/cegueira da nuvem
            NuvemDeTintaLogic logic = cloud.GetComponent<NuvemDeTintaLogic>();
            if (logic == null) logic = cloud.AddComponent<NuvemDeTintaLogic>();

            logic.Setup(_duration);
        }

        // Destroi a granada
        Destroy(gameObject);
    }
}
