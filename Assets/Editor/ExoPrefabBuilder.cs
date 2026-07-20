using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.AI;
using Unity.Cinemachine;
using System.IO;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.Sync;
using UnityEngine.Animations.Rigging;
using ExoBeasts.ExoConfig.Core;

public class ExoPrefabBuilder
{
    // Fase 5: INPUT_ACTIONS_PATH/INPUT_ACTIONS_PATH_ALT e o "using
    // UnityEngine.InputSystem" removidos - eram usados so por
    // ConfigureAsCharacter (tambem removido nesta fase). PlayerInput.actions
    // do Personagem agora vem herdado de profile.basePrefab (ver
    // BuildOrUpdateCharacterVariant) - nunca mais resolvido/atribuido aqui.

    public static void BuildCharacterPrefab(string fbxPath, string prefabFolder, string matFolder)
    {
        BuildCharacterPrefab(fbxPath, prefabFolder, matFolder, null, "");
    }

    public static void BuildCharacterPrefab(string fbxPath, string prefabFolder, string matFolder, ExoPrefabProfile profile)
    {
        BuildCharacterPrefab(fbxPath, prefabFolder, matFolder, profile, "");
    }

    public static void BuildCharacterPrefab(string fbxPath, string prefabFolder, string matFolder, ExoPrefabProfile profile, string categoria)
    {
        BuildCharacterPrefab(fbxPath, prefabFolder, matFolder, profile, categoria, null);
    }

    /// <summary>
    /// Fase 5: overload com ExoBuildReport opcional (default null, preserva
    /// compatibilidade com os overloads acima e com qualquer chamador
    /// existente). BuildPrefabStep.cs passa context.Report explicitamente -
    /// e o unico jeito de Warnings de ApplyAbilityScripts (ex.: MonoScript
    /// que nao resolve para Component valido) chegarem no relatorio
    /// estruturado do pipeline em vez de só Debug.LogWarning.
    /// </summary>
    public static void BuildCharacterPrefab(string fbxPath, string prefabFolder, string matFolder, ExoPrefabProfile profile, string categoria, ExoBuildReport report)
    {
        GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxModel == null)
        {
            Debug.LogError("[ExoConfig] FBX nao encontrado em: " + fbxPath);
            return;
        }

        string entityName = fbxModel.name;
        string prefabPath = Path.Combine(prefabFolder, entityName + " Variant.prefab").Replace("\\", "/");
        if (profile == null || profile.entityType != ExoEntityType.Personagem)
            prefabPath = Path.Combine(prefabFolder, entityName + ".prefab").Replace("\\", "/");

        if (!Directory.Exists(matFolder)) Directory.CreateDirectory(matFolder);
        if (!Directory.Exists(prefabFolder)) Directory.CreateDirectory(prefabFolder);

        ExoEntityType entityType = ExoEntityType.Personagem;
        if (categoria == "Monstros") entityType = ExoEntityType.Monstro;
        else if (categoria == "Environment") entityType = ExoEntityType.Edificio;

        if (profile != null) entityType = profile.entityType;

        Material mat = BuildMaterial(fbxPath, matFolder, entityName, profile, entityType);

        if (entityType == ExoEntityType.Personagem)
        {
            GameObject savedCharPrefab = BuildOrUpdateCharacterVariant(fbxModel, mat, profile, entityName, prefabPath, report);
            if (savedCharPrefab == null)
            {
                // Error ja reportado (Debug.LogError + report.Error) dentro de
                // BuildOrUpdateCharacterVariant - profile ausente ou sem
                // basePrefab. Aborta ANTES de montar a Torre ou de tocar o
                // vinculo em CharacterBase: sem fallback silencioso (mesmo
                // espirito da Fase 4) - nao monta uma Torre "orfa" de um
                // Personagem que falhou, e sobretudo nao sobrescreve
                // commanderPrefab/towerPrefab existentes com valores
                // obsoletos/nulos so porque esta execucao falhou.
                return;
            }
            Debug.Log("[ExoConfig] Prefab Personagem montado: " + prefabPath);

            string towerName = "Torreta" + char.ToUpper(entityName[0]) + entityName.Substring(1);
            string towerPath = Path.Combine(prefabFolder, towerName + ".prefab").Replace("\\", "/");
            GameObject originalTower = FindOriginalPrefab(towerPath, towerName, prefabFolder);

            GameObject fbxTower = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
            ApplyMaterial(fbxTower, mat);
            GameObject towerRoot = ConfigureAsTower(fbxTower, profile, towerName);

            if (originalTower != null)
            {
                CopySerializedValuesAndRelink(originalTower, towerRoot);
                Debug.Log("[ExoConfig] Referencias copiadas do template original para a Torre.");
            }

            PrefabUtility.SaveAsPrefabAsset(towerRoot, towerPath);
            Object.DestroyImmediate(towerRoot);
            Debug.Log("[ExoConfig] Prefab Torre montado: " + towerPath);

            if (profile != null && profile.characterData != null)
            {
                // savedCharPrefab ja veio de BuildOrUpdateCharacterVariant (o
                // GameObject asset devolvido por PrefabUtility.SaveAsPrefabAsset)
                // - nao precisa recarregar via AssetDatabase.LoadAssetAtPath.
                GameObject savedTowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(towerPath);

                profile.characterData.commanderPrefab = savedCharPrefab;
                profile.characterData.towerPrefab = savedTowerPrefab;
                
                EditorUtility.SetDirty(profile.characterData);
                AssetDatabase.SaveAssets();
                Debug.Log("[ExoConfig] Vinculado prefabs no CharacterBase: " + profile.characterData.name);
            }
        }
        else
        {
            GameObject originalEnemy = FindOriginalPrefab(prefabPath, entityName, prefabFolder);

            GameObject fbxInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
            ApplyMaterial(fbxInstance, mat);
            GameObject prefabRoot;
            
            if (entityType == ExoEntityType.Monstro)
                prefabRoot = ConfigureAsEnemy(fbxInstance, profile, entityName);
            else if (entityType == ExoEntityType.Edificio)
            {
                ConfigureAsBuilding(fbxInstance, profile);
                prefabRoot = fbxInstance;
            }
            else
                prefabRoot = fbxInstance;

            if (originalEnemy != null && entityType == ExoEntityType.Monstro)
            {
                CopySerializedValuesAndRelink(originalEnemy, prefabRoot);
                Debug.Log("[ExoConfig] Referencias copiadas do template original para o Monstro.");
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            Object.DestroyImmediate(prefabRoot);
            Debug.Log("[ExoConfig] Prefab montado: " + prefabPath);
        }

        AssetDatabase.Refresh();

        if (entityType != ExoEntityType.Edificio)
        {
            Debug.LogWarning("[ExoConfig] ACAO NECESSARIA: Arraste os prefabs para Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset para funcionar em rede.");
        }
    }

    /// <summary>
    /// Fase 5 da refatoracao Exo Config: substitui ConfigureAsCharacter +
    /// SetupCameraHierarchy (removidos - ver historico no fim deste
    /// comentario) para a metade de PERSONAGEM de BuildCharacterPrefab. A
    /// metade de TORRE (ConfigureAsTower, mais abaixo) e o Monstro
    /// (ConfigureAsEnemy) NAO mudam nesta fase - continuam reconstruindo a
    /// hierarquia do zero e usando CopySerializedValuesAndRelink.
    ///
    /// Estrategia nova (Personagem apenas): em vez de montar uma hierarquia
    /// do zero e salvar como prefab PLANO, instancia profile.basePrefab
    /// (PrefabUtility.InstantiatePrefab) - que ja tem Pivot, SwordPoint,
    /// CameraTarget, CM_Normal, CM_Aim e todos os componentes de
    /// personagem/rede (CharacterController, NetworkObject, PlayerMovement,
    /// PlayerHealthSystem, PlayerShooting, MeleeCombatSystem,
    /// PlayerCombatManager, CommanderAbilityController, PlayerInput,
    /// ClientNetworkTransform, PlayerNetworkSetup, NavMeshObstacle,
    /// ShaderInteractor, SpiderWebDebuffPlayer, VerificadorQueda,
    /// LocalPlayerInputBridge etc. - CONFIRMADO via inspecao do YAML de
    /// Assets/Personagens/Player 1.prefab nesta fase, nao um pressuposto) -
    /// e so troca o modelo sob Pivot + material (+ Animator.runtimeAnimatorController,
    /// se o profile tiver um). PrefabUtility.SaveAsPrefabAsset sobre uma
    /// instancia que se origina de InstantiatePrefab produz nativamente um
    /// Prefab Variant de basePrefab (confirmado empiricamente via script de
    /// diagnostico de scratch nesta fase, ver relato da Fase 5) - a heranca
    /// resolve sozinha, sem precisar de CopySerializedValuesAndRelink.
    ///
    /// IMPORTANTE (confirmado via YAML, nao suposto): no Player 1.prefab
    /// REAL, SwordPoint/CameraTarget/CM_Normal/CM_Aim sao IRMAOS de Pivot
    /// (filhos do root), nao filhos de Pivot como o ConfigureAsCharacter
    /// antigo construia. Como agora herdamos a hierarquia do basePrefab em
    /// vez de reconstrui-la, esse detalhe do codigo antigo deixa de importar
    /// - a estrutura real do basePrefab e que manda, e MeleeCombatSystem.attackPoint
    /// (que ja aponta pra SwordPoint dentro de Player 1.prefab) vem de graca,
    /// sem nenhum relink manual.
    ///
    /// basePrefab e OBRIGATORIO: ver guard clause logo no inicio deste
    /// metodo e o comentario em ExoPrefabProfile.basePrefab.
    ///
    /// Devolve o GameObject do prefab ASSET salvo (o mesmo que
    /// PrefabUtility.SaveAsPrefabAsset devolve - nao uma instancia de cena),
    /// ou null se abortou por falta de basePrefab.
    /// </summary>
    private static GameObject BuildOrUpdateCharacterVariant(GameObject fbxModel, Material mat, ExoPrefabProfile profile, string entityName, string prefabPath, ExoBuildReport report)
    {
        if (profile == null || profile.basePrefab == null)
        {
            string msg = "[ExoConfig] basePrefab nao configurado no ExoPrefabProfile de \"" + entityName + "\". Prefab de Personagem NAO foi criado/atualizado - sem fallback silencioso (configure ExoPrefabProfile.basePrefab, ex.: Assets/Personagens/Player 1.prefab).";
            Debug.LogError(msg);
            report?.Error(msg, entityName);
            return null;
        }

        bool alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;

        // LoadPrefabContents (update-in-place) preserva o vinculo de Variant
        // do proprio arquivo ja salvo; InstantiatePrefab(basePrefab) (criacao)
        // e o que faz SaveAsPrefabAsset produzir um Variant NOVO, ligado a
        // basePrefab - ver comentario da classe acima. Ambos devolvem um
        // GameObject "vivo" e editavel, so a origem/ciclo-de-vida difere
        // (por isso os dois branches do finally abaixo).
        GameObject root = alreadyExists
            ? PrefabUtility.LoadPrefabContents(prefabPath)
            : (GameObject)PrefabUtility.InstantiatePrefab(profile.basePrefab);

        try
        {
            ReplaceModelUnderPivot(root, fbxModel, profile);
            ApplyMaterial(root, mat);

            // Achado confirmado via YAML real (Player 1.prefab e Samurai
            // Variant.prefab) durante o teste de scratch desta fase: objetos
            // customizados presos a um osso do MODELO (ex.: "AimTarget_Fixo",
            // "GripRig", "GripRig hint", "firepoint" minusculo dentro de
            // Samurai Variant.prefab - todos filhos, via m_Father, de um osso
            // DENTRO da instancia aninhada do modelo, nao irmaos de Pivot) sao
            // destruidos junto quando o modelo troca, porque Unity destroi a
            // subarvore inteira do filho antigo de Pivot. Qualquer campo que
            // apontava para dentro dessa subarvore (confirmado no scratch:
            // PlayerMovement.aimRig/aimTarget/aimConstraint,
            // PlayerShooting.firePoint; Samurai Variant.prefab real tambem
            // tem WeaponGripIK.gripRig na mesma situacao) fica nulo. Isso NAO
            // e uma regressao introduzida por esta fase - o ConfigureAsCharacter
            // antigo tinha a MESMA lacuna (CopySerializedValuesAndRelink so
            // copia COMPONENTES para objetos que ja existem no alvo por
            // caminho igual; nunca cria um GameObject novo - "AimTarget_Fixo"
            // nao existiria no charRoot recem-construido do zero, logo nunca
            // seria encontrado por CopyComponentsAndRelink e seria
            // silenciosamente descartado mesmo pelo codigo antigo) - mas nunca
            // tinha sido confirmado nem documentado explicitamente antes desta
            // fase. Sem fallback silencioso: avisar em vez de deixar essas
            // referencias quebrarem sem ninguem perceber.
            string rigWarning = "[ExoConfig] Modelo sob Pivot foi trocado em \"" + entityName + "\" - qualquer referencia que apontava para DENTRO do modelo antigo " +
                "(ex.: PlayerMovement.aimRig/aimTarget/aimConstraint, PlayerShooting.firePoint, WeaponGripIK.gripRig, ou objetos customizados presos a um osso do " +
                "modelo antigo, como \"AimTarget_Fixo\"/\"GripRig\"/\"GripRig hint\") pode ter ficado nula ou ter sido removida - confira manualmente antes de publicar.";
            Debug.LogWarning(rigWarning);
            report?.Warning(rigWarning, entityName);

            // Ability scripts e o rename do root so acontecem na CRIACAO.
            // Update-in-place e a garantia central pedida nesta fase: "troca
            // so modelo sob Pivot + material + Animator.runtimeAnimatorController,
            // nada mais e tocado" - reexportar um modelo nao deve
            // adicionar/alterar componentes de habilidade nem renomear um
            // prefab que o game designer ja pode ter customizado.
            if (!alreadyExists)
            {
                ApplyAbilityScripts(root, profile, entityName, report);
                root.name = entityName + " Variant";
            }

            return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            if (alreadyExists)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Troca o(s) filho(s) de "Pivot" pelo modelo novo (fbxModel), sem tocar
    /// em nenhum outro filho do root (SwordPoint/CameraTarget/CM_Normal/CM_Aim
    /// - todos irmaos de Pivot, nao filhos - ver comentario de
    /// BuildOrUpdateCharacterVariant). Destroi os filhos existentes de Pivot
    /// ANTES de instanciar o novo (snapshot em array antes de destruir, nunca
    /// um while sobre childCount - ver
    /// Assets/Editor/ExoConfig/... e a memoria do projeto
    /// "feedback_destroy_childcount.md": DestroyImmediate em contexto de
    /// Editor e sincrono, mas ainda assim mutar uma colecao enquanto itera e
    /// fragil por construcao).
    ///
    /// Cria Pivot se o basePrefab nao tiver um por algum motivo (defesa - nao
    /// deveria acontecer com Player 1.prefab, mas evita NullReferenceException
    /// se um basePrefab customizado no futuro nao seguir a convencao).
    /// </summary>
    private static void ReplaceModelUnderPivot(GameObject root, GameObject fbxModel, ExoPrefabProfile profile)
    {
        Transform pivot = root.transform.Find("Pivot");
        if (pivot == null)
        {
            GameObject pivotGo = new GameObject("Pivot");
            pivotGo.transform.SetParent(root.transform, false);
            pivotGo.layer = root.layer;
            pivot = pivotGo.transform;
        }

        Transform[] existingChildren = new Transform[pivot.childCount];
        for (int i = 0; i < existingChildren.Length; i++)
            existingChildren[i] = pivot.GetChild(i);
        foreach (Transform child in existingChildren)
            Object.DestroyImmediate(child.gameObject);

        GameObject fbxInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
        fbxInstance.transform.SetParent(pivot, false);
        fbxInstance.layer = pivot.gameObject.layer;

        SetupMeshChildComponents(fbxInstance, profile);
    }

    /// <summary>
    /// Fase 5, item 4 do escopo: substitui o array fixo "logicScripts" (13
    /// nomes, que o ConfigureAsCharacter antigo adicionava via
    /// System.Type.GetType a QUALQUER Personagem - inclusive scripts de
    /// outro personagem, ex.: a Ayame recebia VooGraciosoLogic/
    /// CacadoraNoturnaLogic, que sao da Sylvie - Assets/Personagens/Sylvie/CoreScripts/
    /// - confirmado nesta fase) por uma lista type-safe POR PERFIL
    /// (profile.abilityScripts, MonoScript[] - sobrevive a rename de classe,
    /// ao contrario de Type.GetType("Nome, Assembly-CSharp")).
    ///
    /// So chamado no caminho de CRIACAO (ver BuildOrUpdateCharacterVariant).
    ///
    /// MonoScript.GetClass() devolve null se o script nao compilou ou nao
    /// define nenhuma classe correspondente ao nome do arquivo. Nesses casos,
    /// e quando a classe resolvida nao e um Component (ex.: um
    /// ScriptableObject foi arrastado no array por engano), registra Warning
    /// no report e pula - nunca lanca excecao, nunca trava o pipeline (mesma
    /// filosofia defensiva de CopyComponentsAndRelink com Missing Script).
    /// </summary>
    private static void ApplyAbilityScripts(GameObject root, ExoPrefabProfile profile, string entityName, ExoBuildReport report)
    {
        if (profile.abilityScripts == null) return;

        foreach (MonoScript script in profile.abilityScripts)
        {
            if (script == null) continue;

            System.Type type = script.GetClass();
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                string msg = "[ExoConfig] Ability script \"" + script.name + "\" nao resolve para um Component valido - pulando.";
                Debug.LogWarning(msg);
                report?.Warning(msg, entityName);
                continue;
            }

            EnsureComponent(root, type);
        }
    }

    private static GameObject ConfigureAsTower(GameObject fbxInstance, ExoPrefabProfile profile, string towerName)
    {
        GameObject root = new GameObject(towerName);
        int layer = LayerMask.NameToLayer("Towers");
        if (layer == -1) layer = 0;
        root.layer = layer;

        MeshFilter mf = EnsureComponent<MeshFilter>(root);
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        EnsureComponent<BoxCollider>(root);
        EnsureComponent<SphereCollider>(root);
        EnsureComponent<NetworkObject>(root);
        EnsureComponent<NavMeshObstacle>(root);

        EnsureComponent<GridPlacement>(root);
        EnsureComponent<TowerController>(root);
        EnsureComponent<TowerSelectionCircle>(root);
        EnsureComponent<NetworkedBuilding>(root);
        EnsureComponent<SpiderWebDebuffTower>(root);

        GameObject goEmpty = new GameObject("GameObject");
        goEmpty.transform.SetParent(root.transform, false);

        GameObject circulo = new GameObject("CirculoSeletor");
        circulo.transform.SetParent(root.transform, false);

        fbxInstance.transform.SetParent(root.transform, false);
        SetupMeshChildComponents(fbxInstance, profile);

        return root;
    }

    private static GameObject ConfigureAsEnemy(GameObject fbxInstance, ExoPrefabProfile profile, string entityName)
    {
        GameObject root = new GameObject(entityName);
        root.tag = profile != null ? profile.gameObjectTag : "Enemy";
        int layer = profile != null ? profile.gameObjectLayer : LayerMask.NameToLayer("Enemy");
        if (layer == -1) layer = 0;
        root.layer = layer;

        GameObject popup = GetOrCreateChild(root, "DamagePopupPosition");
        popup.layer = layer;
        popup.transform.localPosition = new Vector3(-0.23f, 1.78f, 0.69f);
        EnsureComponent<RectTransform>(popup);

        Transform existingSphere = root.transform.Find("Sphere");
        if (existingSphere == null)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sphere";
            sphere.transform.SetParent(root.transform, false);
            sphere.layer = layer;
            sphere.transform.localPosition = new Vector3(0.02f, 0.83f, 0.68f);
            sphere.transform.localScale = new Vector3(0.66f, 0.66f, 0.66f);
        }

        GameObject aggro = GetOrCreateChild(root, "Indicador_Aggro");
        aggro.layer = 0;
        aggro.transform.localPosition = new Vector3(0.10f, 2.00f, 0.69f);
        aggro.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        EnsureComponent<SpriteRenderer>(aggro);
        EnsureComponent<FaceCameraBillboard>(aggro);

        GameObject vfx = GetOrCreateChild(root, "Dissolvevfx");
        vfx.layer = 0;
        System.Type vfxType = System.Type.GetType("UnityEngine.VFX.VisualEffect, Unity.VisualEffectGraph.Runtime");
        if (vfxType != null) EnsureComponent(vfx, vfxType);
        
        System.Type binderType = System.Type.GetType("UnityEngine.VFX.Utility.VFXPropertyBinder, Unity.VisualEffectGraph.Runtime");
        if (binderType != null) EnsureComponent(vfx, binderType);

        fbxInstance.transform.SetParent(root.transform, false);

        CapsuleCollider col = EnsureComponent<CapsuleCollider>(root);
        col.center = profile != null ? profile.enemyCapsuleCenter : new Vector3(-0.03f, 0.98f, 0.07f);
        col.radius = profile != null ? profile.enemyCapsuleRadius : 1.46f;
        col.height = profile != null ? profile.enemyCapsuleHeight : 2.92f;

        EnsureComponent<Rigidbody>(root);

        EnemyController enemyController = EnsureComponent<EnemyController>(root);
        if (profile != null && profile.enemyData != null)
            enemyController.originalMoveSpeed = profile.enemyData.moveSpeed;

        EnemyHealthSystem enemyHealth = EnsureComponent<EnemyHealthSystem>(root);
        if (profile != null)
            enemyHealth.enemyData = profile.enemyData;

        EnsureComponent<NetworkObject>(root);

        EnemyCombatSystem combatSystem = EnsureComponent<EnemyCombatSystem>(root);
        if (profile != null)
        {
            combatSystem.playerLayer = profile.enemyPlayerLayer;
            combatSystem.towerLayer = profile.enemyTowerLayer;
        }

        Animator anim = EnsureComponent<Animator>(root);
        if (profile != null && profile.animatorController != null)
            anim.runtimeAnimatorController = profile.animatorController;

        NavMeshAgent agent = EnsureComponent<NavMeshAgent>(root);
        if (profile != null && profile.enemyData != null)
        {
            agent.speed = profile.enemyData.moveSpeed;
        }
        agent.stoppingDistance = 1.5f;
        agent.radius = col.radius;
        agent.height = col.height;

        EnsureComponent<NetworkTransform>(root);
        EnsureComponent<NetworkedEnemy>(root);
        
        NetworkAnimator netAnim = EnsureComponent<NetworkAnimator>(root);
        netAnim.Animator = anim;

        EnsureComponent<ShaderInteractor>(root);
        root.AddComponent<ShaderInteractor>();
        
        System.Type bleedType = System.Type.GetType("EnemyBleedAttack, Assembly-CSharp");
        if (bleedType != null) EnsureComponent(root, bleedType);

        return root;
    }

    private static void ConfigureAsBuilding(GameObject root, ExoPrefabProfile profile)
    {
        System.Type navMeshModifierType = System.Type.GetType("Unity.AI.Navigation.NavMeshModifier, Unity.AI.Navigation");
        if (navMeshModifierType != null)
        {
            Component modifier = root.GetComponent(navMeshModifierType) ?? root.AddComponent(navMeshModifierType);
            var overrideField = navMeshModifierType.GetField("m_OverrideArea", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var areaField = navMeshModifierType.GetField("m_Area", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var applyToChildrenField = navMeshModifierType.GetField("m_ApplyToChildren", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (overrideField != null) overrideField.SetValue(modifier, true);
            if (areaField != null) areaField.SetValue(modifier, 1);
            if (applyToChildrenField != null) applyToChildrenField.SetValue(modifier, true);
        }
    }

    private static void SetupMeshChildComponents(GameObject meshObj, ExoPrefabProfile profile)
    {
        Animator anim = meshObj.GetComponent<Animator>();
        if (anim == null) anim = meshObj.AddComponent<Animator>();
        
        if (profile != null && profile.animatorController != null)
            anim.runtimeAnimatorController = profile.animatorController;
        
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        System.Type proxyType = System.Type.GetType("AnimationEventProxy, Assembly-CSharp");
        if (proxyType != null) EnsureComponent(meshObj, proxyType);

        NetworkAnimator netAnim = EnsureComponent<NetworkAnimator>(meshObj);
        netAnim.Animator = anim;

        RigBuilder builder = EnsureComponent<RigBuilder>(meshObj);
        Rig[] rigs = meshObj.GetComponentsInChildren<Rig>(true);
        if (rigs != null && rigs.Length > 0)
        {
            builder.layers.Clear();
            foreach (Rig r in rigs)
            {
                builder.layers.Add(new RigLayer(r));
            }
        }
    }

    /// <summary>
    /// Shader usado para o material de TODAS as categorias (decisao da Fase
    /// 4 da refatoracao Exo Config). Antes desta fase, Environment tentava
    /// "Toon/Toon" primeiro. NENHUM asset do projeto (Assets/) declara esse
    /// nome, mas Shader.Find("Toon/Toon") NAO retornava null mesmo assim:
    /// verificado em runtime (Fase 4, via script de diagnostico temporario)
    /// que ele resolve para
    /// Packages/com.unity.toonshader/Runtime/Integrated/Shaders/UnityToon.shader
    /// - o shader generico do pacote oficial "com.unity.toonshader",
    /// instalado neste projeto mas SEM NENHUMA RELACAO com o
    /// ToonExobeasts.shadergraph proprio do ExoBeasts. Ou seja: o fallback
    /// para "Universal Render Pipeline/Lit" (o segundo Shader.Find, removido
    /// nesta fase) era CODIGO MORTO - nunca executava, porque o primeiro
    /// Shader.Find ja "tinha sucesso" contra o shader errado. O bug real
    /// nao era "cai em silencio pro URP/Lit", e sim "usa em silencio um
    /// shader toon GENERICO de terceiros, visualmente inconsistente com o
    /// toon shading proprio do projeto" - uma inconsistencia pior de
    /// diagnosticar (o material nao fica magenta/quebrado, so "parece" toon
    /// e nao bate com o resto). Personagens/Monstros ja usavam "Shader
    /// Graphs/ToonExobeasts" (existe em
    /// Assets/ShadersGlobais/Toon/ToonExobeasts.shadergraph, confirmado via
    /// m_Path: "Shader Graphs" e os m_DefaultReferenceName do proprio
    /// .shadergraph - _BaseMap, _shadingMap, _ShadowColor,
    /// _OuterShadowColor, _OuterShadowWidth, _LightSmooth, _FlashAmount,
    /// _FlashColor - todos usados abaixo). Unificar para as 3 categorias
    /// elimina o fallback silencioso: se este shader nao existir,
    /// BuildMaterial agora reporta Error (via ExoBuildReport) e retorna
    /// null em vez de produzir um material com shader errado que parece
    /// certo.
    /// </summary>
    internal const string ToonShaderName = "Shader Graphs/ToonExobeasts";

    /// <summary>
    /// Cria ou atualiza o material da entidade. "internal" (era "private"
    /// ate a Fase 4): tambem chamado por
    /// Assets/Editor/ExoConfig/Pipeline/Steps/MaterialStep.cs, no mesmo
    /// assembly implicito (Assembly-CSharp-Editor) - MaterialStep e quem
    /// decide ABORTAR o pipeline se "report" vier com HasErrors=true (ver
    /// ExoBuildPipeline.Run). A chamada interna ja existente logo abaixo, em
    /// BuildCharacterPrefab, continua identica e sem "report" (fica null por
    /// padrao) - por construcao do pipeline (steps executam em ordem, param
    /// no primeiro erro), essa segunda chamada so acontece depois que
    /// MaterialStep ja validou o shader com sucesso e ja criou/atualizou o
    /// material; quando isso ocorre, esta funcao apenas reatualiza as mesmas
    /// propriedades no material ja existente (idempotente - nao recria, nao
    /// troca GUID).
    ///
    /// Fase 4 tambem corrigiu dois problemas aqui: (1) sempre recriava o
    /// material via AssetDatabase.CreateAsset, trocando o GUID e quebrando
    /// qualquer referencia externa ja apontando para ele - agora, se o
    /// material ja existe em matPath, atualiza as propriedades no objeto
    /// existente em vez de recriar; (2) "_MainTex" era setado a toa -
    /// ToonExobeasts nao expõe essa propriedade (confirmado: nenhum
    /// m_DefaultReferenceName do .shadergraph e "_MainTex"), entao
    /// Material.SetTexture virava no-op silencioso (a API nao lanca erro
    /// para propriedade inexistente no shader ativo). Isso so fazia sentido
    /// enquanto Environment podia cair no fallback de Universal Render
    /// Pipeline/Lit (que tem _MainTex de verdade); sem fallback, nunca mais
    /// faz sentido manter, entao foi removido.
    /// </summary>
    internal static Material BuildMaterial(string fbxPath, string matFolder, string entityName, ExoPrefabProfile profile, ExoEntityType entityType, ExoBuildReport report = null)
    {
        string matPath = Path.Combine(matFolder, entityName + "_Mat.mat").Replace("\\", "/");

        Shader shader = Shader.Find(ToonShaderName);
        if (shader == null)
        {
            report?.Error("Shader \"" + ToonShaderName + "\" nao encontrado no projeto. Material nao foi criado/atualizado - sem fallback silencioso para outro shader.", entityName);
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        bool isNew = mat == null;
        if (isNew)
            mat = new Material(shader);
        else if (mat.shader != shader)
            mat.shader = shader;

        Texture2D baseTex = null;
        if (profile != null && profile.baseMapTexture != null) baseTex = profile.baseMapTexture;
        else
        {
            string texturePath = fbxPath.Replace("Modelos", "Texturas").Replace(".fbx", "T.png");
            baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }

        if (baseTex != null)
        {
            mat.SetTexture("_BaseMap", baseTex);
        }

        if (profile != null && entityType != ExoEntityType.Edificio)
        {
            if (profile.shadingMapTexture != null) mat.SetTexture("_shadingMap", profile.shadingMapTexture);
            mat.SetColor("_ShadowColor", profile.shadowColor);
            mat.SetColor("_OuterShadowColor", profile.outerShadowColor);
            mat.SetFloat("_OuterShadowWidth", profile.outerShadowWidth);
            mat.SetFloat("_LightSmooth", profile.lightSmooth);
            mat.SetFloat("_FlashAmount", 0f);
            mat.SetColor("_FlashColor", Color.white);
        }

        if (isNew)
            AssetDatabase.CreateAsset(mat, matPath);
        else
            EditorUtility.SetDirty(mat);

        AssetDatabase.SaveAssets();
        report?.Info("Material " + (isNew ? "criado" : "atualizado") + " em: " + matPath, entityName);
        return AssetDatabase.LoadAssetAtPath<Material>(matPath);
    }

    private static void ApplyMaterial(GameObject instance, Material mat)
    {
        if (mat == null) return;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            Material[] slots = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            r.sharedMaterials = slots;
        }
    }

    private static GameObject FindOriginalPrefab(string prefabPath, string entityName, string folder)
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (go != null) return go;

        string cleanEntity = entityName.Replace("Torreta", "").Replace("Variant", "").Replace("Completo", "").Trim();
        cleanEntity = System.Text.RegularExpressions.Regex.Replace(cleanEntity, @"\d+$", "").Trim();

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            
            if (name.ToLower().Contains(cleanEntity.ToLower()))
            {
                bool lookingForTower = entityName.StartsWith("Torreta");
                bool isTowerPrefab = name.StartsWith("Torreta");
                
                if (lookingForTower == isTowerPrefab)
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
        }
        return null;
    }

    private static void CopySerializedValuesAndRelink(GameObject sourceRoot, GameObject targetRoot)
    {
        string origFbxName = "";
        Transform sourcePivot = sourceRoot.transform.Find("Pivot");
        if (sourcePivot != null && sourcePivot.childCount > 0)
        {
            origFbxName = sourcePivot.GetChild(0).name;
        }

        string newFbxName = "";
        Transform targetPivot = targetRoot.transform.Find("Pivot");
        if (targetPivot != null && targetPivot.childCount > 0)
        {
            newFbxName = targetPivot.GetChild(0).name;
        }

        CopyComponentsAndRelink(sourceRoot, targetRoot, sourceRoot, targetRoot, origFbxName, newFbxName);

        Transform[] originalTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform origT in originalTransforms)
        {
            if (origT == sourceRoot.transform) continue;
            string path = GetRelativePath(sourceRoot.transform, origT);
            string mappedPath = MapRelativePath(path, origFbxName, newFbxName);
            
            Transform newT = targetRoot.transform.Find(mappedPath);
            if (newT != null)
            {
                CopyComponentsAndRelink(origT.gameObject, newT.gameObject, sourceRoot, targetRoot, origFbxName, newFbxName);
            }
        }
    }

    private static void CopyComponentsAndRelink(GameObject sourceGo, GameObject targetGo, GameObject sourceRoot, GameObject targetRoot, string origFbxName, string newFbxName)
    {
        Component[] sourceComponents = sourceGo.GetComponents<Component>();
        foreach (Component sourceComp in sourceComponents)
        {
            if (sourceComp == null) continue;
            
            try
            {
                if (sourceComp is MonoBehaviour)
                {
                    SerializedObject tempSO = new SerializedObject(sourceComp);
                    SerializedProperty scriptProp = tempSO.FindProperty("m_Script");
                    if (scriptProp != null && scriptProp.objectReferenceValue == null)
                    {
                        Debug.LogWarning("[ExoConfig] Script perdido (Missing Script) detectado no objeto " + sourceGo.name + ". Pulando.");
                        continue;
                    }
                }
            }
            catch
            {
                continue;
            }

            if (sourceComp is Transform sourceTrans)
            {
                Transform targetTrans = targetGo.transform;
                if (sourceGo == sourceRoot)
                {
                    targetTrans.localScale = sourceTrans.localScale;
                }
                else
                {
                    targetTrans.localPosition = sourceTrans.localPosition;
                    targetTrans.localRotation = sourceTrans.localRotation;
                    targetTrans.localScale = sourceTrans.localScale;
                }
                continue;
            }

            System.Type type = sourceComp.GetType();
            Component targetComp = targetGo.GetComponent(type);
            if (targetComp == null)
            {
                try
                {
                    targetComp = targetGo.AddComponent(type);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[ExoConfig] Nao foi possivel adicionar o componente " + type.Name + " em " + targetGo.name + ": " + ex.Message);
                }
            }

            if (targetComp == null) continue;

            SerializedObject sourceSO = new SerializedObject(sourceComp);
            SerializedObject targetSO = new SerializedObject(targetComp);

            SerializedProperty prop = sourceSO.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.name == "m_ObjectHideFlags" || prop.name == "m_CorrespondingSourceObject" || 
                        prop.name == "m_PrefabInstance" || prop.name == "m_PrefabAsset" || prop.name == "m_GameObject" ||
                        prop.name == "m_Enabled")
                        continue;

                    SerializedProperty targetProp = targetSO.FindProperty(prop.name);
                    if (targetProp == null) continue;

                    CopyPropertyAndRelink(prop, targetProp, sourceRoot, targetRoot, origFbxName, newFbxName);
                } while (prop.NextVisible(false));
            }
            targetSO.ApplyModifiedProperties();
        }
    }

    private static void CopyPropertyAndRelink(SerializedProperty sourceProp, SerializedProperty targetProp, GameObject sourceRoot, GameObject targetRoot, string origFbxName, string newFbxName)
    {
        if (sourceProp.propertyType == SerializedPropertyType.ObjectReference)
        {
            Object refObj = sourceProp.objectReferenceValue;
            if (refObj == null)
            {
                targetProp.objectReferenceValue = null;
            }
            else if (refObj is GameObject refGo)
            {
                if (IsChildOf(sourceRoot, refGo))
                {
                    string relPath = GetRelativePath(sourceRoot.transform, refGo.transform);
                    string mappedPath = MapRelativePath(relPath, origFbxName, newFbxName);
                    
                    if (mappedPath == refGo.name && refGo == sourceRoot)
                    {
                        targetProp.objectReferenceValue = targetRoot;
                    }
                    else
                    {
                        Transform found = targetRoot.transform.Find(mappedPath);
                        targetProp.objectReferenceValue = found != null ? found.gameObject : null;
                    }
                }
                else
                {
                    targetProp.objectReferenceValue = refObj;
                }
            }
            else if (refObj is Component refComp)
            {
                if (IsChildOf(sourceRoot, refComp.gameObject))
                {
                    string relPath = GetRelativePath(sourceRoot.transform, refComp.gameObject.transform);
                    string mappedPath = MapRelativePath(relPath, origFbxName, newFbxName);
                    
                    GameObject targetTargetGo = null;
                    if (mappedPath == refComp.gameObject.name && refComp.gameObject == sourceRoot)
                    {
                        targetTargetGo = targetRoot;
                    }
                    else
                    {
                        Transform found = targetRoot.transform.Find(mappedPath);
                        if (found != null) targetTargetGo = found.gameObject;
                    }

                    if (targetTargetGo != null)
                    {
                        targetProp.objectReferenceValue = targetTargetGo.GetComponent(refComp.GetType());
                    }
                }
                else
                {
                    targetProp.objectReferenceValue = refObj;
                }
            }
            else
            {
                targetProp.objectReferenceValue = refObj;
            }
        }
        else if (sourceProp.isArray && sourceProp.propertyType != SerializedPropertyType.String)
        {
            targetProp.arraySize = sourceProp.arraySize;
            for (int i = 0; i < sourceProp.arraySize; i++)
            {
                CopyPropertyAndRelink(sourceProp.GetArrayElementAtIndex(i), targetProp.GetArrayElementAtIndex(i), sourceRoot, targetRoot, origFbxName, newFbxName);
            }
        }
        else
        {
            switch (sourceProp.propertyType)
            {
                case SerializedPropertyType.Integer: targetProp.intValue = sourceProp.intValue; break;
                case SerializedPropertyType.Boolean: targetProp.boolValue = sourceProp.boolValue; break;
                case SerializedPropertyType.Float: targetProp.floatValue = sourceProp.floatValue; break;
                case SerializedPropertyType.String: targetProp.stringValue = sourceProp.stringValue; break;
                case SerializedPropertyType.Color: targetProp.colorValue = sourceProp.colorValue; break;
                case SerializedPropertyType.Enum: targetProp.enumValueIndex = sourceProp.enumValueIndex; break;
                case SerializedPropertyType.Vector2: targetProp.vector2Value = sourceProp.vector2Value; break;
                case SerializedPropertyType.Vector3: targetProp.vector3Value = sourceProp.vector3Value; break;
                case SerializedPropertyType.Vector4: targetProp.vector4Value = sourceProp.vector4Value; break;
                case SerializedPropertyType.Rect: targetProp.rectValue = sourceProp.rectValue; break;
                case SerializedPropertyType.Bounds: targetProp.boundsValue = sourceProp.boundsValue; break;
                case SerializedPropertyType.AnimationCurve: targetProp.animationCurveValue = sourceProp.animationCurveValue; break;
            }
        }
    }

    private static string MapRelativePath(string origPath, string origFbxName, string newFbxName)
    {
        if (string.IsNullOrEmpty(origFbxName) || string.IsNullOrEmpty(newFbxName)) return origPath;
        
        string origToken = "Pivot/" + origFbxName;
        string newToken = "Pivot/" + newFbxName;
        
        if (origPath.StartsWith(origToken))
        {
            return newToken + origPath.Substring(origToken.Length);
        }
        return origPath;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        string path = target.name;
        Transform parent = target.parent;
        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private static bool IsChildOf(GameObject root, GameObject target)
    {
        if (target == root) return true;
        Transform parent = target.transform.parent;
        while (parent != null)
        {
            if (parent.gameObject == root) return true;
            parent = parent.parent;
        }
        return false;
    }

    private static Component EnsureComponent(GameObject go, System.Type type)
    {
        Component existing = go.GetComponent(type);
        if (existing != null) return existing;
        return go.AddComponent(type);
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        if (existing != null) return existing;
        return go.AddComponent<T>();
    }

    private static GameObject GetOrCreateChild(GameObject parent, string childName)
    {
        Transform existing = parent.transform.Find(childName);
        if (existing != null) return existing.gameObject;
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent.transform, false);
        return child;
    }
}
