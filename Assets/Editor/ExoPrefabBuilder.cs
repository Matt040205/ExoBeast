using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.AI;
using Unity.Cinemachine;
using System.IO;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.Sync;
using UnityEngine.Animations.Rigging;

public class ExoPrefabBuilder
{
    private const string INPUT_ACTIONS_PATH = "Assets/Configuracoes/Settings/InputSystem_Actions.inputactions";
    private const string INPUT_ACTIONS_PATH_ALT = "Assets/Configurações/Settings/InputSystem_Actions.inputactions";

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
            GameObject originalChar = FindOriginalPrefab(prefabPath, entityName, prefabFolder);

            GameObject fbxChar = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
            ApplyMaterial(fbxChar, mat);
            GameObject charRoot = ConfigureAsCharacter(fbxChar, profile, entityName + " Variant");
            
            if (originalChar != null)
            {
                CopySerializedValuesAndRelink(originalChar, charRoot);
                Debug.Log("[ExoConfig] Referencias copiadas do template original para o Personagem.");
            }

            PrefabUtility.SaveAsPrefabAsset(charRoot, prefabPath);
            Object.DestroyImmediate(charRoot);
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
                GameObject savedCharPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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

    private static GameObject ConfigureAsCharacter(GameObject fbxInstance, ExoPrefabProfile profile, string entityName)
    {
        GameObject root = new GameObject(entityName);
        root.tag = profile != null ? profile.gameObjectTag : "Player";
        int layer = profile != null ? profile.gameObjectLayer : LayerMask.NameToLayer("Player");
        if (layer == -1) layer = 0;
        root.layer = layer;

        GameObject pivot = new GameObject("Pivot");
        pivot.transform.SetParent(root.transform, false);
        pivot.layer = layer;

        fbxInstance.transform.SetParent(pivot.transform, false);

        GameObject swordPoint = new GameObject("SwordPoint");
        swordPoint.transform.SetParent(pivot.transform, false);
        swordPoint.transform.localPosition = new Vector3(0f, 0f, 1f);
        swordPoint.layer = layer;

        SetupMeshChildComponents(fbxInstance, profile);

        CharacterController cc = EnsureComponent<CharacterController>(root);
        cc.center = profile != null ? profile.capsuleCenter : new Vector3(0f, 1f, 0f);
        cc.radius = profile != null ? profile.capsuleRadius : 0.3f;
        cc.height = profile != null ? profile.capsuleHeight : 2f;

        PlayerMovement movement = EnsureComponent<PlayerMovement>(root);
        if (profile != null && profile.characterData != null)
        {
            movement.walkSpeed = profile.characterData.moveSpeed;
            movement.runSpeed = profile.characterData.moveSpeed * 1.8f;
        }

        PlayerHealthSystem health = EnsureComponent<PlayerHealthSystem>(root);
        if (profile != null) health.characterData = profile.characterData;

        PlayerShooting shooting = EnsureComponent<PlayerShooting>(root);
        if (profile != null)
        {
            shooting.characterData = profile.characterData;
            shooting.hitLayers = profile.playerHitLayers;
        }

        MeleeCombatSystem melee = EnsureComponent<MeleeCombatSystem>(root);
        if (profile != null)
        {
            melee.characterData = profile.characterData;
            melee.hitLayers = profile.meleeHitLayers;
        }

        PlayerCombatManager combatManager = EnsureComponent<PlayerCombatManager>(root);
        if (profile != null) combatManager.characterData = profile.characterData;
        combatManager.shootingSystem = shooting;
        combatManager.meleeSystem = melee;
        combatManager.healthSystem = health;

        CommanderAbilityController abilityCtrl = EnsureComponent<CommanderAbilityController>(root);
        if (profile != null) abilityCtrl.characterData = profile.characterData;

        SetupCameraHierarchy(root);

        EnsureComponent<VerificadorQueda>(root);
        EnsureComponent<LocalPlayerInputBridge>(root);

        PlayerInput playerInput = EnsureComponent<PlayerInput>(root);
        InputActionAsset actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(INPUT_ACTIONS_PATH_ALT);
        if (actionsAsset == null)
            actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(INPUT_ACTIONS_PATH);
        if (actionsAsset != null)
        {
            playerInput.actions = actionsAsset;
            playerInput.defaultActionMap = "Player";
        }

        EnsureComponent<NetworkObject>(root);
        EnsureComponent<ClientNetworkTransform>(root);
        EnsureComponent<PlayerNetworkSetup>(root);

        EnsureComponent<NavMeshObstacle>(root);
        EnsureComponent<ShaderInteractor>(root);
        EnsureComponent<SpiderWebDebuffPlayer>(root);
        root.AddComponent<SpiderWebDebuffPlayer>();

        string[] logicScripts = {
            "DebugDamage", "CommanderController", "InventarioFrascos", "PauseControl", "WeaponGripIK", "MeshTrail",
            "VooGraciosoLogic", "CacadoraNoturnaLogic", "PeaceOfMindLogic", "CuttingBladeLogic", 
            "NineTailsDanceLogic", "HealingSkillController", "HealVFXReactor"
        };
        foreach(var scriptName in logicScripts)
        {
            System.Type t = System.Type.GetType(scriptName + ", Assembly-CSharp");
            if (t == null) t = System.Type.GetType(scriptName);
            if (t != null) EnsureComponent(root, t);
        }

        return root;
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

    private static void SetupCameraHierarchy(GameObject root)
    {
        GameObject cameraTargetObj = GetOrCreateChild(root, "CameraTarget");
        cameraTargetObj.layer = root.layer;

        CameraController camCtrl = EnsureComponent<CameraController>(cameraTargetObj);

        GameObject cmNormalObj = GetOrCreateChild(root, "CM_Normal");
        cmNormalObj.transform.localPosition = new Vector3(0f, 1.5f, -4f);
        CinemachineCamera cmNormal = EnsureComponent<CinemachineCamera>(cmNormalObj);
        CinemachineThirdPersonFollow normalFollow = EnsureComponent<CinemachineThirdPersonFollow>(cmNormalObj);
        normalFollow.ShoulderOffset = new Vector3(0f, 1.8f, 0f);
        normalFollow.CameraDistance = 4f;
        EnsureComponent<CinemachineImpulseListener>(cmNormalObj);

        GameObject cmAimObj = GetOrCreateChild(root, "CM_Aim");
        cmAimObj.transform.localPosition = new Vector3(1.16f, 0.33f, -2f);
        CinemachineCamera cmAim = EnsureComponent<CinemachineCamera>(cmAimObj);
        CinemachineThirdPersonFollow aimFollow = EnsureComponent<CinemachineThirdPersonFollow>(cmAimObj);
        aimFollow.ShoulderOffset = new Vector3(1.16f, 0.33f, -2f);
        aimFollow.CameraDistance = 2f;
        EnsureComponent<CinemachineImpulseListener>(cmAimObj);

        camCtrl.normalCamera = cmNormal;
        camCtrl.aimCamera = cmAim;
    }

    private static Material BuildMaterial(string fbxPath, string matFolder, string entityName, ExoPrefabProfile profile, ExoEntityType entityType)
    {
        string matPath = Path.Combine(matFolder, entityName + "_Mat.mat").Replace("\\", "/");

        string shaderName = "Shader Graphs/ToonExobeasts";
        if (entityType == ExoEntityType.Edificio) shaderName = "Toon/Toon";

        Shader shader = Shader.Find(shaderName);
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        Material mat = new Material(shader);

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
            mat.SetTexture("_MainTex", baseTex);
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

        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
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
