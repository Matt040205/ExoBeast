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

    [Tooltip("Prefab do efeito visual giratório da lógica")]
    public ObraPrimaLogic logicPrefab;

    [Header("Visual")]
    [Tooltip("Prefab do efeito visual (shader) da ultimate")]
    public GameObject ultimateVfxPrefab;

    public override bool Activate(GameObject quemUsou)
    {   
        if (logicPrefab == null) return true;

        ObraPrimaLogic logic = Instantiate(logicPrefab, quemUsou.transform);
        logic.transform.localPosition = Vector3.zero;
        logic.transform.localRotation = Quaternion.identity;
        logic.StartUltimate(quemUsou, duracao, quantidadeTiros, danoPorTiro, raio, duracaoSilencio, true, ultimateVfxPrefab);

        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();
        controller?.StartLocalObraPrimaOwnerProxy(duracao, quantidadeTiros, danoPorTiro, raio, duracaoSilencio);

        return true;
    }
}
