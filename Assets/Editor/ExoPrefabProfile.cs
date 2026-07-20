using UnityEngine;
using UnityEditor;

public enum ExoEntityType { Personagem, Monstro, Edificio }

[CreateAssetMenu(fileName = "ExoPrefabProfile", menuName = "Exo Config/Perfil de Prefab")]
public class ExoPrefabProfile : ScriptableObject
{
    [Header("Tipo de Entidade")]
    public ExoEntityType entityType = ExoEntityType.Personagem;

    // Fase 5 da refatoracao Exo Config: estrategia de Prefab Variant para
    // Personagem (ver Assets/Editor/ExoPrefabBuilder.cs,
    // BuildOrUpdateCharacterVariant). basePrefab e OBRIGATORIO quando
    // entityType == Personagem - sem ele, o builder reporta Error e nao
    // cria/atualiza nada (sem fallback silencioso, mesmo espirito da decisao
    // de shader da Fase 4). Nenhuma das 4 entidades reais de Personagem
    // (Ayame/Brunhilde/Coral/Sylvie) tem profile hoje em ExoToolConfig.asset
    // (confirmado - ProfileAssetPath vazio nas 4) - configurar um profile com
    // basePrefab (ex.: Assets/Personagens/Player 1.prefab) e um pre-requisito
    // novo para usar a ferramenta em Personagem a partir desta fase.
    [Header("Prefab Base - Prefab Variant nativo (Personagem)")]
    [Tooltip("Prefab do qual o Personagem herda via Prefab Variant nativo (ex.: Assets/Personagens/Player 1.prefab). OBRIGATORIO quando entityType == Personagem.")]
    public GameObject basePrefab;

    [Tooltip("Scripts de habilidade especificos desta entidade (ex.: VooGraciosoLogic para a Sylvie, NAO para a Ayame). Adicionados ao root do Personagem somente na CRIACAO (entidade nova) - update-in-place preserva os componentes de habilidade ja existentes sem mexer. Cada MonoScript precisa resolver para um Component valido; senao um Warning e registrado no report e o script e pulado.")]
    public MonoScript[] abilityScripts;

    [Header("Dados")]
    public CharacterBase characterData;
    public EnemyDataSO enemyData;

    [Header("Animacoes")]
    [Tooltip("Override manual do Animator Controller. Se vazio, AnimatorStep (Fase 7) resolve por convencao: \"<Nome>Animator.controller\" dentro da pasta Animacao da entidade (ex.: Assets/Personagens/Ayame/Animação/AyameAnimator.controller). Controllers sao sempre autorais - nem este campo nem a convencao criam um novo.")]
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
