using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ControladorTrilhaMonstro : MonoBehaviour
{
    [Header("Configuração do Caminho")]
    [Tooltip("Arraste os pontos (GameObjects vazios) que formam a rota do monstro para esta lista.")]
    public Transform[] pontosDaRota;

    [Header("Ajuste de Altura")]
    [Tooltip("Eleva a linha um pouco acima do chão para evitar que ela 'entre' na textura do piso (Z-fighting).")]
    public float alturaDoChao = 0.2f;

    private LineRenderer renderizadorDeLinha;

    void Awake()
    {
        // Obtém automaticamente o componente Line Renderer anexado a este objeto
        renderizadorDeLinha = GetComponent<LineRenderer>();
    }

    void Start()
    {
        DesenharCaminho();
    }

    public void DesenharCaminho()
    {
        // Validação de segurança: Se não houver pontos suficientes, a linha não é desenhada
        if (pontosDaRota == null || pontosDaRota.Length < 2)
        {
            Debug.LogWarning("Pontos da rota insuficientes no objeto " + gameObject.name);
            return;
        }

        // Define a quantidade de "nós" que a linha terá
        renderizadorDeLinha.positionCount = pontosDaRota.Length;

        // Percorre a lista de pontos e define a posição de cada nó da linha no espaço 3D
        for (int i = 0; i < pontosDaRota.Length; i++)
        {
            // Pega a posição do ponto atual
            Vector3 posicaoDoPonto = pontosDaRota[i].position;

            // Adiciona a pequena elevação para a linha não bugar no chão
            posicaoDoPonto.y += alturaDoChao;

            // Insere a coordenada na linha
            renderizadorDeLinha.SetPosition(i, posicaoDoPonto);
        }
    }
}