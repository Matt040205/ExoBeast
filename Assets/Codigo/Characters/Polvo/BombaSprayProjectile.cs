using UnityEngine;

public class BombaSprayProjectile : MonoBehaviour
{
    private float _radius;
    private float _duration; // Duração da nuvem

    [Header("Configurações da Explosão")]
    [Tooltip("Prefab da nuvem de gás que aparece quando explode")]
    public GameObject gasCloudPrefab;

    [Header("Configurações de Tempo")]
    [Tooltip("Tempo máximo até explodir sozinho (se nunca bater em nada)")]
    public float tempoMaximoVida = 7f;

    [Tooltip("Tempo para explodir APÓS bater na primeira coisa")]
    public float tempoAposImpacto = 3f;

    private bool jaBateu = false;
    private bool jaExplodiu = false;

    // Configura os dados quando é lançada (chamado pela Habilidade)
    public void Launch(Vector3 velocity, float radius, float cloudDuration)
    {
        GetComponent<Rigidbody>().linearVelocity = velocity;
        _radius = radius;
        _duration = cloudDuration;

        // 1. Inicia o timer de segurança (7s)
        Invoke(nameof(Explode), tempoMaximoVida);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Se já bateu ou já explodiu, ignora batidas subsequentes (quicar no chão)
        if (jaBateu || jaExplodiu) return;

        jaBateu = true;

        // 2. Cancela o timer de 7s, porque agora vale o timer do impacto
        CancelInvoke(nameof(Explode));

        // 3. Inicia o timer de impacto (3s)
        Invoke(nameof(Explode), tempoAposImpacto);
    }

    void Explode()
    {
        // Garante que não exploda duas vezes
        if (jaExplodiu) return;
        jaExplodiu = true;

        // Cria a nuvem de gás no local da granada
        if (gasCloudPrefab != null)
        {
            Quaternion rot = Quaternion.identity;

            // Instancia a nuvem
            GameObject cloud = Instantiate(gasCloudPrefab, transform.position, rot);

            // Configura o tamanho
            cloud.transform.localScale = Vector3.one * _radius;

            // Inicializa a lógica de dano/cegueira da nuvem
            NuvemDeTintaLogic logic = cloud.GetComponent<NuvemDeTintaLogic>();
            if (logic == null) logic = cloud.AddComponent<NuvemDeTintaLogic>();

            logic.Setup(_duration);
        }

        // Destroi a granada
        Destroy(gameObject);
    }
}