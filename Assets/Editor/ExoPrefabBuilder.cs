using UnityEngine;
using UnityEditor;
using System.IO;

public class ExoPrefabBuilder
{
    public static void BuildCharacterPrefab(string fbxPath, string prefabFolder, string matFolder)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null) return;

        string prefabPath = Path.Combine(prefabFolder, model.name + ".prefab").Replace("\\", "/");
        string matPath = Path.Combine(matFolder, model.name + "_Mat.mat").Replace("\\", "/");
        string texturePath = fbxPath.Replace("Modelos", "Texturas").Replace(".fbx", "T.png");

        if (!Directory.Exists(matFolder)) Directory.CreateDirectory(matFolder);
        if (!Directory.Exists(prefabFolder)) Directory.CreateDirectory(prefabFolder);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex != null) mat.SetTexture("_BaseMap", tex);

        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        Renderer rend = instance.GetComponentInChildren<Renderer>();
        if (rend != null) rend.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        AssetDatabase.Refresh();
        Debug.Log("Prefab montado: " + prefabPath);
    }
}