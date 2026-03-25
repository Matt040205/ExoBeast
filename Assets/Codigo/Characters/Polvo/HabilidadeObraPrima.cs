using UnityEngine;

[CreateAssetMenu(fileName = "Obra-Prima do Caos", menuName = "ExoBeasts/Personagens/Polvo/Habilidade/Obra-Prima do Caos")]
public class HabilidadeObraPrima : Ability
{
    [Header("Configuração da Ultimate")]
    public float duracao = 5f;
    public int quantidadeTiros = 10;   // <--- NOVO: Quantos hits vai dar
    public float danoPorTiro = 15f;    // <--- NOVO: Dano de cada hit
    public float raio = 8f;
    public float duracaoSilencio = 2f; // <--- Certifique-se de que essa variável existe

    [Tooltip("Prefab do efeito visual giratório")]
    public ObraPrimaLogic logicPrefab;

    public override bool Activate(GameObject quemUsou)
    {   
        if (logicPrefab == null) return true;

        ObraPrimaLogic logic = Instantiate(logicPrefab, quemUsou.transform);
        logic.StartUltimate(quemUsou, duracao, quantidadeTiros, danoPorTiro, raio, duracaoSilencio);

        return true;
    }
}