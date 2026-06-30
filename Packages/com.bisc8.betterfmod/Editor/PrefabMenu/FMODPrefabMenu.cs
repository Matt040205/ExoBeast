using UnityEditor;
using UnityEngine;

public static class FMODPrefabMenu
{
    private const string PackagePrefabsPath = "Packages/com.bisc8.betterfmod/Runtime/FmodSystem/Prefabs_FMOD";

    [MenuItem("GameObject/BISC8 FMOD/Fmod Emitter Mng", false, 10)]
    private static void CreateFmodEmitter(MenuCommand command)
    {
        CreatePrefab("FmodEmitter_Mng.prefab", command);
    }

    [MenuItem("GameObject/BISC8 FMOD/Fmod Slider Mng", false, 11)]
    private static void CreateFmodSlider(MenuCommand command)
    {
        CreatePrefab("FmodSlider_Mng.prefab", command);
    }

    [MenuItem("GameObject/BISC8 FMOD/Fmod System OUT", false, 12)]
    private static void CreateFmodSystemOut(MenuCommand command)
    {
        CreatePrefab("Fmod_System OUT.prefab", command);
    }

    private static void CreatePrefab(string prefabFileName, MenuCommand command)
    {
        GameObject prefab = LoadPrefab(prefabFileName);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "BISC8 Better FMOD",
                $"Prefab not found: {prefabFileName}",
                "OK"
            );
            return;
        }

        Object instance = PrefabUtility.InstantiatePrefab(prefab);
        if (instance is not GameObject gameObject)
            return;

        GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {prefab.name}");
        Selection.activeGameObject = gameObject;
    }

    private static GameObject LoadPrefab(string prefabFileName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackagePrefabsPath}/{prefabFileName}");
        if (prefab != null)
            return prefab;

        return null;
    }
}
