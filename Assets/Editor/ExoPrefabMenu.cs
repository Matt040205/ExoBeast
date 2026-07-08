using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System;

public class ExoPrefabMenu
{
    public static void ExecutarOrganizar(string categoria, string nome)
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null) return;

        string sourcePath = AssetDatabase.GetAssetPath(selected);
        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        string folderPath = Path.GetDirectoryName(sourcePath);

        string prefix = categoria + "_" + nome + "_";
        string tModels = EditorPrefs.GetString(prefix + "Mod");
        string tTextures = EditorPrefs.GetString(prefix + "Tex");
        string tPrefabs = EditorPrefs.GetString(prefix + "Pre");
        string tMaterials = EditorPrefs.GetString(prefix + "Mat");

        if (string.IsNullOrEmpty(tModels) || string.IsNullOrEmpty(tPrefabs))
        {
            Debug.LogError($"Diretórios não configurados para {nome}. Verifique o Exo Config.");
            return;
        }

        string destModel = Path.Combine(tModels, fileName + ".fbx").Replace("\\", "/");
        AssetDatabase.MoveAsset(sourcePath, destModel);

        string sourceTex = Path.Combine(folderPath, fileName + "T.png").Replace("\\", "/");
        if (File.Exists(sourceTex))
        {
            AssetDatabase.MoveAsset(sourceTex, Path.Combine(tTextures, fileName + "T.png").Replace("\\", "/"));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ExoPrefabBuilder.BuildCharacterPrefab(destModel, tPrefabs, tMaterials);
    }

    public static void GenerateMenus()
    {
        string path = "Assets/Editor/ExoGeneratedMenus.cs";
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("// ARQUIVO GERADO AUTOMATICAMENTE. NÃO EDITE.");
        sb.AppendLine("using UnityEditor;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("");
        sb.AppendLine("public static class ExoGeneratedMenus");
        sb.AppendLine("{");

        AppendMenuMethods(sb, "Personagens", "Entities/Characters/");
        AppendMenuMethods(sb, "Monstros", "Entities/Enemies/");
        AppendMenuMethods(sb, "Environment", "Environment/");

        sb.AppendLine("}");

        File.WriteAllText(path, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("Menus customizados atualizados com sucesso!");
    }

    private static void AppendMenuMethods(StringBuilder sb, string categoria, string menuPath)
    {
        string rawList = EditorPrefs.GetString(categoria, "");
        if (string.IsNullOrEmpty(rawList)) return;

        string[] entities = rawList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string entity in entities)
        {
            // Remove qualquer caractere que não seja letra ou número para evitar quebrar o código C#
            string safeName = Regex.Replace(entity, "[^a-zA-Z0-9_]", "");

            sb.AppendLine($"    [MenuItem(\"Assets/Exo Prefabs/{menuPath}{entity}\", true)]");
            sb.AppendLine($"    static bool Val_{categoria}_{safeName}()");
            sb.AppendLine("    {");
            sb.AppendLine("        string path = AssetDatabase.GetAssetPath(Selection.activeObject);");
            sb.AppendLine("        return !string.IsNullOrEmpty(path) && Path.GetExtension(path).ToLower() == \".fbx\";");
            sb.AppendLine("    }");

            sb.AppendLine($"    [MenuItem(\"Assets/Exo Prefabs/{menuPath}{entity}\", false, 20)]");
            sb.AppendLine($"    public static void Org_{categoria}_{safeName}()");
            sb.AppendLine("    {");
            sb.AppendLine($"        ExoPrefabMenu.ExecutarOrganizar(\"{categoria}\", \"{entity}\");");
            sb.AppendLine("    }");
        }
    }
}