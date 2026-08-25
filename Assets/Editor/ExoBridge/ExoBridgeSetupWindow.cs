using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Assistente obrigatorio para os dados que o Blender nao consegue inferir:
/// perfis, ScriptableObjects, componentes e mapeamentos de clips/slots.
/// </summary>
public sealed class ExoBridgeSetupWindow : EditorWindow
{
    private int _selectedEntryIndex;
    private SerializedObject _profileSerializedObject;
    private ExoPrefabProfile _profile;

    [MenuItem("Exo Bridge/Configurar perfis", false, 1001)]
    public static void ShowWindow()
    {
        GetWindow<ExoBridgeSetupWindow>("Exo Bridge Profiles");
    }

    private void OnGUI()
    {
        ExoToolConfig config = ExoToolConfig.Load();
        if (config == null || config.Entries.Count == 0)
        {
            EditorGUILayout.HelpBox("ExoToolConfig nao existe ou nao possui entidades cadastradas.", MessageType.Error);
            return;
        }

        List<ExoToolConfigEntry> entries = new List<ExoToolConfigEntry>(config.Entries);
        string[] labels = entries.ConvertAll(entry => entry.Definition.Categoria + " / " + entry.Definition.Nome).ToArray();
        _selectedEntryIndex = Mathf.Clamp(_selectedEntryIndex, 0, entries.Count - 1);
        int newIndex = EditorGUILayout.Popup("Entidade", _selectedEntryIndex, labels);
        if (newIndex != _selectedEntryIndex)
        {
            _selectedEntryIndex = newIndex;
            _profile = null;
            _profileSerializedObject = null;
        }

        ExoToolConfigEntry entry = entries[_selectedEntryIndex];
        ExoCategoryParser.TryParse(entry.Definition.Categoria, out ExoCategory category);
        if (_profile == null)
        {
            _profile = string.IsNullOrEmpty(entry.ProfileAssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(entry.ProfileAssetPath);
        }

        if (_profile == null)
        {
            EditorGUILayout.HelpBox("Esta entidade ainda nao tem perfil. A ponte nao permite importacao ate que um perfil seja criado e configurado explicitamente.", MessageType.Warning);
            if (GUILayout.Button("Criar ExoPrefabProfile"))
                CreateProfile(config, entry, category);
            return;
        }

        DrawProfile(entry, category);
    }

    private void CreateProfile(ExoToolConfig config, ExoToolConfigEntry entry, ExoCategory category)
    {
        string prefabsFolder = config.ResolveFolder(category, entry.Definition.Nome, ExoAssetType.Prefabs);
        Directory.CreateDirectory(ExoBridgeManifestReader.ToFullPath(prefabsFolder));
        AssetDatabase.Refresh();

        string path = ExoPathResolver.Normalize(Path.Combine(prefabsFolder, entry.Definition.Nome + " ExoBridgeProfile.asset"));
        if (AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(path) != null)
        {
            EditorUtility.DisplayDialog("Exo Bridge", "Ja existe um perfil em " + path, "OK");
            return;
        }

        _profile = CreateInstance<ExoPrefabProfile>();
        _profile.entityType = category == ExoCategory.Personagens ? ExoEntityType.Personagem
            : category == ExoCategory.Monstros ? ExoEntityType.Monstro
            : ExoEntityType.Edificio;
        AssetDatabase.CreateAsset(_profile, path);
        config.SetProfileAssetPath(category, entry.Definition.Nome, path);
        AssetDatabase.SaveAssets();
        _profileSerializedObject = new SerializedObject(_profile);
        Selection.activeObject = _profile;
    }

    private void DrawProfile(ExoToolConfigEntry entry, ExoCategory category)
    {
        if (_profileSerializedObject == null)
            _profileSerializedObject = new SerializedObject(_profile);
        _profileSerializedObject.Update();

        EditorGUILayout.LabelField("Perfil vinculado", AssetDatabase.GetAssetPath(_profile));
        EditorGUILayout.PropertyField(_profileSerializedObject.FindProperty("entityType"));
        EditorGUILayout.PropertyField(_profileSerializedObject.FindProperty("basePrefab"));
        EditorGUILayout.PropertyField(_profileSerializedObject.FindProperty("characterData"));
        EditorGUILayout.PropertyField(_profileSerializedObject.FindProperty("enemyData"));
        EditorGUILayout.PropertyField(_profileSerializedObject.FindProperty("abilityScripts"), includeChildren: true);
        EditorGUILayout.PropertyField(_profileSerializedObject.FindProperty("animatorController"));
        EditorGUILayout.PropertyField(_profileSerializedObject.FindProperty("materialSlotBindings"), includeChildren: true);
        EditorGUILayout.PropertyField(_profileSerializedObject.FindProperty("animationBindings"), includeChildren: true);

        if (_profileSerializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssetIfDirty(_profile);
        }

        if (_profile.animatorController != null && !(_profile.animatorController is AnimatorOverrideController))
        {
            EditorGUILayout.HelpBox("O bridge nao altera maquinas de estados. Crie um AnimatorOverrideController a partir deste controller para poder mapear Actions com seguranca.", MessageType.Warning);
            if (GUILayout.Button("Criar AnimatorOverrideController"))
                CreateOverrideController(entry);
        }

        EditorGUILayout.HelpBox(
            "Confirme basePrefab, ScriptableObjects, scripts de habilidade, bindings de material e bindings de Action antes de importar. Esses dados Unity-only nunca sao inferidos do Blender.",
            MessageType.Info);
    }

    private void CreateOverrideController(ExoToolConfigEntry entry)
    {
        string profilePath = AssetDatabase.GetAssetPath(_profile);
        string directory = Path.GetDirectoryName(profilePath);
        string output = ExoPathResolver.Normalize(Path.Combine(directory, entry.Definition.Nome + " ExoBridge.overrideController"));
        AnimatorOverrideController overrideController = new AnimatorOverrideController(_profile.animatorController);
        AssetDatabase.CreateAsset(overrideController, output);
        _profile.animatorController = overrideController;
        EditorUtility.SetDirty(_profile);
        AssetDatabase.SaveAssets();
        _profileSerializedObject = new SerializedObject(_profile);
    }
}
