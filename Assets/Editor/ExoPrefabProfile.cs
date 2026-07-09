using UnityEngine;

public enum ExoEntityType { Personagem, Monstro, Edificio }

[CreateAssetMenu(fileName = "ExoPrefabProfile", menuName = "Exo Config/Perfil de Prefab")]
public class ExoPrefabProfile : ScriptableObject
{
    [Header("Tipo de Entidade")]
    public ExoEntityType entityType = ExoEntityType.Personagem;

    [Header("Dados")]
    public CharacterBase characterData;
    public EnemyDataSO enemyData;

    [Header("Animacoes")]
    public RuntimeAnimatorController animatorController;

    [Header("Material - Shader ToonExobeasts")]
    [Tooltip("Textura principal (BaseMap). Se vazio, busca [Nome]T.png na pasta Texturas.")]
    public Texture2D baseMapTexture;
    [Tooltip("Textura de shading (shadingMap). Opcional.")]
    public Texture2D shadingMapTexture;
    [Tooltip("Cor da sombra (ShadowColor).")]
    public Color shadowColor = new Color(0.4f, 0.4f, 0.8f, 1f);
    [Tooltip("Cor da sombra externa (OuterShadowColor).")]
    public Color outerShadowColor = new Color(1f, 0.7f, 0.8f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Largura da sombra externa (OuterShadowWidth).")]
    public float outerShadowWidth = 0.15f;
    [Range(0f, 0.5f)]
    [Tooltip("Suavidade da luz (LightSmooth).")]
    public float lightSmooth = 0.01f;

    [Header("Fisica e Colisao (Raiz)")]
    public string gameObjectTag = "Player";
    public int gameObjectLayer = 6;
    public Vector3 capsuleCenter = new Vector3(0f, 1f, 0f);
    public float capsuleRadius = 0.3f;
    public float capsuleHeight = 2f;

    [Header("Pontos de Referencia (Personagem)")]
    public Vector3 firePointLocalPosition = new Vector3(0f, 1.4f, 0.6f);
    public Vector3 attackPointLocalPosition = new Vector3(0f, 1f, 1f);

    [Header("Layers de Hit (Personagem)")]
    public LayerMask playerHitLayers;
    public LayerMask meleeHitLayers;

    [Header("Capsula do Inimigo (Monstro)")]
    public Vector3 enemyCapsuleCenter = new Vector3(0f, 0.8f, 0f);
    public float enemyCapsuleRadius = 0.4f;
    public float enemyCapsuleHeight = 1.8f;

    [Header("Ponto de Ataque (Monstro)")]
    public Vector3 enemyAttackPointLocalPosition = new Vector3(0f, 0.8f, 1f);
    public LayerMask enemyPlayerLayer;
    public LayerMask enemyTowerLayer;
}
