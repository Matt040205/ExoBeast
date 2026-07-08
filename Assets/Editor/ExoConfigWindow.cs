using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

public class ExoConfigWindow : EditorWindow
{
    private string currentTab = "Personagens";
    private string newEntityName = "";
    private string selectedEntity = "";

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
        GUILayout.BeginVertical(GUILayout.Width(120));
        if (GUILayout.Button("Personagens")) { currentTab = "Personagens"; selectedEntity = ""; }
        if (GUILayout.Button("Monstros")) { currentTab = "Monstros"; selectedEntity = ""; }
        if (GUILayout.Button("Environment")) { currentTab = "Environment"; selectedEntity = ""; }
        GUILayout.Space(20);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Atualizar Menus", GUILayout.Height(40)))
        {
            ExoPrefabMenu.GenerateMenus();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();

        GUILayout.BeginVertical();
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

        if (GUILayout.Button("Organizar ▼"))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("A-Z"), false, () => SortList((a, b) => a.CompareTo(b)));
            menu.AddItem(new GUIContent("Data Criação (Antigo-Novo)"), false, () => SortList((a, b) => GetTicks("Created", a).CompareTo(GetTicks("Created", b))));
            menu.AddItem(new GUIContent("Data Modificação (Novo-Antigo)"), false, () => SortList((a, b) => GetTicks("Modified", b).CompareTo(GetTicks("Modified", a))));
            menu.ShowAsContext();
        }
        GUILayout.EndHorizontal();

        var entities = GetList(currentTab);
        foreach (var entity in entities.ToList())
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(entity)) selectedEntity = entity;
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                var list = GetList(currentTab);
                list.Remove(entity);
                SaveList(currentTab, list);
                if (selectedEntity == entity) selectedEntity = "";
            }
            GUILayout.EndHorizontal();
        }

        if (!string.IsNullOrEmpty(selectedEntity))
        {
            string prefix = currentTab + "_" + selectedEntity + "_";
            EditorGUI.BeginChangeCheck();

            if (currentTab != "Environment")
                EditorPrefs.SetString(prefix + "Ani", EditorGUILayout.TextField("Animações:", EditorPrefs.GetString(prefix + "Ani")));

            EditorPrefs.SetString(prefix + "Mat", EditorGUILayout.TextField("Materiais:", EditorPrefs.GetString(prefix + "Mat")));
            EditorPrefs.SetString(prefix + "Mod", EditorGUILayout.TextField("Modelos:", EditorPrefs.GetString(prefix + "Mod")));
            EditorPrefs.SetString(prefix + "Pre", EditorGUILayout.TextField("Prefabs:", EditorPrefs.GetString(prefix + "Pre")));
            EditorPrefs.SetString(prefix + "Tex", EditorGUILayout.TextField("Texturas:", EditorPrefs.GetString(prefix + "Tex")));

            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString("Modified_" + currentTab + "_" + selectedEntity, DateTime.Now.Ticks.ToString());
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void SortList(Comparison<string> comparison)
    {
        var list = GetList(currentTab);
        list.Sort(comparison);
        SaveList(currentTab, list);
    }

    private long GetTicks(string type, string name) => long.Parse(EditorPrefs.GetString(type + "_" + currentTab + "_" + name, "0"));
}