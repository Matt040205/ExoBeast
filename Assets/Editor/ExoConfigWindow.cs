using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

public class ExoConfigWindow : EditorWindow
{
    private string currentTab = "Personagens";
    private string newEntityName = "";
    private string selectedEntity = "";

    private static readonly string[] TABS = { "Personagens", "Monstros", "Environment" };

    private List<string> GetList(string key) => new List<string>((EditorPrefs.GetString(key, "")).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

    private void SaveList(string key, List<string> list)
    {
        EditorPrefs.SetString(key, string.Join(",", list));
        ExoPrefabMenu.GenerateMenus();
    }

    [MenuItem("Exo Config/Edit", false, 1000)]
    public static void ShowWindow() => GetWindow<ExoConfigWindow>("Exo Config");

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        DrawSidebar();
        GUILayout.BeginVertical();
        DrawMainPanel();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void DrawSidebar()
    {
        GUILayout.BeginVertical(GUILayout.Width(130));

        foreach (string tab in TABS)
        {
            GUI.backgroundColor = currentTab == tab ? new Color(0.4f, 0.7f, 1f) : Color.white;
            if (GUILayout.Button(tab, GUILayout.Height(30))) { currentTab = tab; selectedEntity = ""; }
        }

        GUI.backgroundColor = Color.white;
        GUILayout.Space(20);
        GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
        if (GUILayout.Button("Atualizar Menus", GUILayout.Height(40)))
            ExoPrefabMenu.GenerateMenus();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawMainPanel()
    {
        GUILayout.Label(currentTab, EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        newEntityName = EditorGUILayout.TextField(newEntityName);

        if (GUILayout.Button("Adicionar"))
        {
            var list = GetList(currentTab);
            if (!string.IsNullOrEmpty(newEntityName) && !list.Contains(newEntityName))
            {
                list.Add(newEntityName);
                EditorPrefs.SetString("Created_" + currentTab + "_" + newEntityName, DateTime.Now.Ticks.ToString());
                EditorPrefs.SetString("Modified_" + currentTab + "_" + newEntityName, DateTime.Now.Ticks.ToString());
                SaveList(currentTab, list);
            }
            newEntityName = "";
        }

        if (GUILayout.Button("Organizar v"))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("A-Z"), false, () => SortList((a, b) => a.CompareTo(b)));
            menu.AddItem(new GUIContent("Data Criacao (Antigo-Novo)"), false, () => SortList((a, b) => GetTicks("Created", a).CompareTo(GetTicks("Created", b))));
            menu.AddItem(new GUIContent("Data Modificacao (Novo-Antigo)"), false, () => SortList((a, b) => GetTicks("Modified", b).CompareTo(GetTicks("Modified", a))));
            menu.ShowAsContext();
        }
        GUILayout.EndHorizontal();

        var entities = GetList(currentTab);
        foreach (var entity in entities.ToList())
        {
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = selectedEntity == entity ? new Color(0.4f, 0.7f, 1f) : Color.white;
            if (GUILayout.Button(entity)) selectedEntity = entity;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                var list = GetList(currentTab);
                list.Remove(entity);
                SaveList(currentTab, list);
                if (selectedEntity == entity) selectedEntity = "";
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        if (!string.IsNullOrEmpty(selectedEntity))
        {
            EditorGUILayout.Space(10);
            DrawEntityConfig(selectedEntity);
        }
    }

    private void DrawEntityConfig(string entity)
    {
        string prefix = currentTab + "_" + entity + "_";

        EditorGUILayout.LabelField("--- Caminhos de Pasta ---", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        if (currentTab != "Environment")
            EditorPrefs.SetString(prefix + "Ani", EditorGUILayout.TextField("Animacoes:", EditorPrefs.GetString(prefix + "Ani")));

        EditorPrefs.SetString(prefix + "Mat", EditorGUILayout.TextField("Materiais:", EditorPrefs.GetString(prefix + "Mat")));
        EditorPrefs.SetString(prefix + "Mod", EditorGUILayout.TextField("Modelos:", EditorPrefs.GetString(prefix + "Mod")));
        EditorPrefs.SetString(prefix + "Pre", EditorGUILayout.TextField("Prefabs:", EditorPrefs.GetString(prefix + "Pre")));
        EditorPrefs.SetString(prefix + "Tex", EditorGUILayout.TextField("Texturas:", EditorPrefs.GetString(prefix + "Tex")));

        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString("Modified_" + currentTab + "_" + entity, DateTime.Now.Ticks.ToString());

        EditorGUILayout.Space(10);
        DrawProfileSection(prefix, entity);
    }

    private void DrawProfileSection(string prefix, string entity)
    {
        EditorGUILayout.LabelField("--- Perfil de Componentes ---", EditorStyles.boldLabel);

        string profileKey = prefix + "Profile";
        string profilePath = EditorPrefs.GetString(profileKey, "");

        ExoPrefabProfile profile = null;
        if (!string.IsNullOrEmpty(profilePath))
            profile = AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(profilePath);

        EditorGUI.BeginChangeCheck();
        ExoPrefabProfile newProfile = (ExoPrefabProfile)EditorGUILayout.ObjectField(
            "Perfil:", profile, typeof(ExoPrefabProfile), false);

        if (EditorGUI.EndChangeCheck())
        {
            if (newProfile != null)
                EditorPrefs.SetString(profileKey, AssetDatabase.GetAssetPath(newProfile));
            else
                EditorPrefs.DeleteKey(profileKey);
        }

        GUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
        if (GUILayout.Button("Criar Perfil"))
        {
            string targetFolder = ResolveProfileFolder(prefix);
            if (!string.IsNullOrEmpty(targetFolder))
            {
                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                string assetPath = targetFolder + "/ExoPrefabProfile_" + entity + ".asset";
                assetPath = assetPath.Replace("\\", "/");

                ExoPrefabProfile existing = AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(assetPath);
                if (existing != null)
                {
                    Debug.LogWarning("[ExoConfig] Perfil ja existe em: " + assetPath);
                    EditorGUIUtility.PingObject(existing);
                }
                else
                {
                    ExoPrefabProfile newAsset = ScriptableObject.CreateInstance<ExoPrefabProfile>();
                    newAsset.entityType = currentTab == "Monstros" ? ExoEntityType.Monstro
                                       : currentTab == "Environment" ? ExoEntityType.Edificio
                                       : ExoEntityType.Personagem;

                    if (currentTab == "Monstros")
                    {
                        newAsset.gameObjectTag = "Enemy";
                        newAsset.gameObjectLayer = 7;
                    }
                    else if (currentTab == "Environment")
                    {
                        newAsset.gameObjectTag = "Untagged";
                        newAsset.gameObjectLayer = 0;
                    }

                    AssetDatabase.CreateAsset(newAsset, assetPath);
                    AssetDatabase.SaveAssets();
                    EditorPrefs.SetString(prefix + "Profile", assetPath);
                    EditorGUIUtility.PingObject(newAsset);
                    Debug.Log("[ExoConfig] Perfil criado em: " + assetPath);
                }
            }
            else
            {
                Debug.LogError("[ExoConfig] Configure a pasta de Prefabs da entidade antes de criar o perfil.");
            }
        }
        GUI.backgroundColor = Color.white;

        if (profile != null)
        {
            GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
            if (GUILayout.Button("Selecionar Perfil"))
                Selection.activeObject = profile;
            GUI.backgroundColor = Color.white;
        }

        GUILayout.EndHorizontal();

        if (profile == null)
        {
            EditorGUILayout.HelpBox(
                "Sem perfil: modo basico (apenas visual + material).\n" +
                "Crie e configure o perfil para prefabs 100% funcionais.",
                MessageType.Info);
        }
        else
        {
            string tipoLabel = profile.entityType == ExoEntityType.Personagem ? "Personagem"
                             : profile.entityType == ExoEntityType.Monstro ? "Monstro"
                             : "Edificio";
            EditorGUILayout.HelpBox(
                "Perfil [" + tipoLabel + "] vinculado. Builder ira adicionar todos os componentes.",
                MessageType.None);
        }
    }

    private string ResolveProfileFolder(string prefix)
    {
        string prefabFolder = EditorPrefs.GetString(prefix + "Pre", "");
        if (!string.IsNullOrEmpty(prefabFolder)) return prefabFolder;
        string modFolder = EditorPrefs.GetString(prefix + "Mod", "");
        if (!string.IsNullOrEmpty(modFolder)) return modFolder;
        return "";
    }

    private void SortList(Comparison<string> comparison)
    {
        var list = GetList(currentTab);
        list.Sort(comparison);
        SaveList(currentTab, list);
    }

    private long GetTicks(string type, string name) => long.Parse(EditorPrefs.GetString(type + "_" + currentTab + "_" + name, "0"));
}
