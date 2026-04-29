using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class FioEletricoPorPonto : MonoBehaviour
{
    [Header("Pontos de Fixação")]
    public Transform pontoDeInicio;
    public Transform pontoDeFim;

    [Header("Configurações Anime / Medium Poly")]
    [Range(2, 30)]
    public int resolucao = 8; // Poucos vértices para manter o estilo low poly
    public float caimento = 0.6f;
    public float larguraDoFio = 0.03f;

    private LineRenderer lr;

    void OnEnable()
    {
        lr = GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (lr == null || pontoDeInicio == null || pontoDeFim == null)
        {
            if (lr != null) lr.positionCount = 0;
            return;
        }

        // Configurações básicas
        lr.positionCount = resolucao;
        lr.startWidth = larguraDoFio;
        lr.endWidth = larguraDoFio;
        lr.useWorldSpace = true;

        // --- O TRUQUE PARA NÃO SUMIR NA DISTÂNCIA ---
        // Pega a câmera principal (em multiplayer, certifique-se de que a câmera do jogador local tem a tag "MainCamera")
        if (Camera.main != null)
        {
            float distanciaCamera = Vector3.Distance(transform.position, Camera.main.transform.position);

            // Se a câmera passar de 15 metros de distância, o fio começa a ficar mais grosso
            // O valor 15f é o raio onde ele mantém o tamanho normal. Ajuste conforme necessário.
            float multiplicador = Mathf.Max(1f, distanciaCamera / 15f);

            lr.widthMultiplier = multiplicador;
        }
        // ---------------------------------------------

        DesenharCatenaria();
    }

    void DesenharCatenaria()
    {
        Vector3 inicio = pontoDeInicio.position;
        Vector3 fim = pontoDeFim.position;

        for (int i = 0; i < resolucao; i++)
        {
            float t = (float)i / (resolucao - 1);

            // Interpolação linear entre os dois pontos
            Vector3 posAtual = Vector3.Lerp(inicio, fim, t);

            // Cálculo da curva (parábola) para simular o peso do fio
            // A fórmula 4 * caimento * t * (1 - t) garante que o centro seja o ponto mais baixo
            float curva = 4 * caimento * t * (1 - t);
            posAtual.y -= curva;

            lr.SetPosition(i, posAtual);
        }
    }
}