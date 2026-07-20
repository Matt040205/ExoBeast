using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Janela de edicao da config da Exo Config. Fase 2: fonte de dados trocada
/// de EditorPrefs para ExoToolConfig (asset versionado - ver
/// Assets/Editor/ExoConfig/ExoToolConfig.cs). A UI em si (layout, botoes,
/// cores) nao foi redesenhada nesta fase - so a fonte dos dados mudou -,
/// exceto pela secao de caminhos de pasta: como os caminhos agora sao
/// DERIVADOS por convencao (ExoPathResolver) em vez de texto livre por
/// entidade, a janela mostra o caminho resolvido e um botao explicito para
/// criar/remover override, em vez de 5 campos de texto sempre editaveis (ver
/// DrawFolderRow).
/// </summary>
public class ExoConfigWindow : EditorWindow
{
    private static readonly ExoCategory[] CATEGORIAS = (ExoCategory[])Enum.GetValues(typeof(ExoCategory));
    private static readonly ExoAssetType[] TIPOS_ASSET = (ExoAssetType[])Enum.GetValues(typeof(ExoAssetType));

    private ExoToolConfig config;
    private ExoCategory currentCategoria = ExoCategory.Personagens;
    private string newEntityName = "";
    private string selectedEntity = "";

    [MenuItem("Exo Config/Edit", false, 1000)]
    public static void ShowWindow() => GetWindow<ExoConfigWindow>("Exo Config");

    private void OnEnable()
    {
        config = ExoToolConfig.LoadOrCreate();
    }

    private void OnGUI()
    {
        if (config == null)
            config = ExoToolConfig.LoadOrCreate();

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

        foreach (ExoCategory categoria in CATEGORIAS)
        {
            GUI.backgroundColor = currentCategoria == categoria ? new Color(0.4f, 0.7f, 1f) : Color.white;
            if (GUILayout.Button(categoria.ToString(), GUILayout.Height(30))) { currentCategoria = categoria; selectedEntity = ""; }
        }

        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawMainPanel()
    {
        GUILayout.Label(currentCategoria.ToString(), EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        newEntityName = EditorGUILayout.TextField(newEntityName);

        if (GUILayout.Button("Adicionar"))
        {
            if (!string.IsNullOrEmpty(newEntityName) && config.FindEntry(currentCategoria, newEntityName) == null)
            {
                config.AddEntity(currentCategoria, newEntityName);
            }
            newEntityName = "";
        }

        if (GUILayout.Button("Organizar v"))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("A-Z"), false, () =>
                config.SortCategoria(currentCategoria, (a, b) => string.Compare(a.Definition.Nome, b.Definition.Nome, StringComparison.Ordinal)));
            menu.AddItem(new GUIContent("Data Criacao (Antigo-Novo)"), false, () =>
                config.SortCategoria(currentCategoria, (a, b) => a.CreatedTicks.CompareTo(b.CreatedTicks)));
            menu.AddItem(new GUIContent("Data Modificacao (Novo-Antigo)"), false, () =>
                config.SortCategoria(currentCategoria, (a, b) => b.ModifiedTicks.CompareTo(a.ModifiedTicks)));
            menu.ShowAsContext();
        }
        GUILayout.EndHorizontal();

        // ToList() tira uma copia antes de iterar: o botao "X" abaixo pode
        // chamar config.RemoveEntity, que muta a lista interna de config -
        // iterar direto sobre config.GetByCategoria (IEnumerable preguicoso
        // via LINQ Where) enquanto ela e mutada lançaria
        // InvalidOperationException. Mesmo cuidado que o codigo original ja
        // tinha (GetList(currentTab).ToList()) sobre a lista do EditorPrefs.
        List<ExoToolConfigEntry> entidades = config.GetByCategoria(currentCategoria).ToList();
        foreach (ExoToolConfigEntry entry in entidades)
        {
            string entity = entry.Definition.Nome;
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = selectedEntity == entity ? new Color(0.4f, 0.7f, 1f) : Color.white;
            if (GUILayout.Button(entity)) selectedEntity = entity;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                config.RemoveEntity(currentCategoria, entity);
                if (selectedEntity == entity) selectedEntity = "";
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        if (!string.IsNullOrEmpty(selectedEntity))
        {
            ExoToolConfigEntry selected = config.FindEntry(currentCategoria, selectedEntity);
            if (selected != null)
            {
                EditorGUILayout.Space(10);
                DrawEntityConfig(selected);
            }
            else
            {
                // Selecionada foi removida (ex.: pelo botao X acima nesta
                // mesma passada de OnGUI) - limpa a selecao em vez de
                // desenhar uma secao para uma entidade que nao existe mais.
                selectedEntity = "";
            }
        }
    }

    private void DrawEntityConfig(ExoToolConfigEntry entry)
    {
        EditorGUILayout.LabelField("--- Caminhos de Pasta ---", EditorStyles.boldLabel);

        foreach (ExoAssetType tipo in TIPOS_ASSET)
        {
            if (!ExoPathResolver.SupportsAssetType(currentCategoria, tipo))
                continue;

            DrawFolderRow(entry, tipo);
        }

        EditorGUILayout.Space(10);
        DrawProfileSection(entry);
    }

    /// <summary>
    /// Uma linha de caminho de pasta. Sem override: mostra o caminho
    /// resolvido pela convencao (ExoPathResolver via config.ResolveFolder),
    /// campo desabilitado/cinza, com botao "Sobrescrever" que semeia um
    /// override igual ao caminho atual (ponto de partida sao para editar).
    /// Com override: campo editavel com fundo amarelo e sufixo "[override]",
    /// mais botao "Reverter" que remove o override e volta pra convencao.
    /// Satisfaz o pedido da Fase 2 de "mostrar o caminho resolvido e permitir
    /// override explicito" e "marcar visualmente quando um valor e override
    /// vs convencao", sem pedir os 5 caminhos na mao como antes.
    /// </summary>
    private void DrawFolderRow(ExoToolConfigEntry entry, ExoAssetType tipo)
    {
        string nome = entry.Definition.Nome;
        string label = LabelFor(tipo);
        bool hasOverride = entry.TryGetFolderOverride(tipo, out string overridePasta);

        GUILayout.BeginHorizontal();

        if (hasOverride)
        {
            GUI.backgroundColor = new Color(1f, 0.92f, 0.55f);
            EditorGUI.BeginChangeCheck();
            string edited = EditorGUILayout.TextField(label + " [override]", overridePasta);
            if (EditorGUI.EndChangeCheck())
                config.SetFolderOverride(currentCategoria, nome, tipo, edited);
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Reverter", GUILayout.Width(70)))
                config.ClearFolderOverride(currentCategoria, nome, tipo);
        }
        else
        {
            string resolved = config.ResolveFolder(currentCategoria, nome, tipo);
            GUI.enabled = false;
            EditorGUILayout.TextField(label + " (convencao)", resolved);
            GUI.enabled = true;

            if (GUILayout.Button("Sobrescrever", GUILayout.Width(90)))
                config.SetFolderOverride(currentCategoria, nome, tipo, resolved);
        }

        GUILayout.EndHorizontal();
    }

    private static string LabelFor(ExoAssetType tipo)
    {
        switch (tipo)
        {
            case ExoAssetType.Animacao: return "Animacoes:";
            case ExoAssetType.Materiais: return "Materiais:";
            case ExoAssetType.Modelos: return "Modelos:";
            case ExoAssetType.Prefabs: return "Prefabs:";
            case ExoAssetType.Texturas: return "Texturas:";
            default: return tipo + ":";
        }
    }

    private void DrawProfileSection(ExoToolConfigEntry entry)
    {
        EditorGUILayout.LabelField("--- Perfil de Componentes ---", EditorStyles.boldLabel);

        ExoPrefabProfile profile = null;
        if (!string.IsNullOrEmpty(entry.ProfileAssetPath))
            profile = AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(entry.ProfileAssetPath);

        EditorGUI.BeginChangeCheck();
        ExoPrefabProfile newProfile = (ExoPrefabProfile)EditorGUILayout.ObjectField(
            "Perfil:", profile, typeof(ExoPrefabProfile), false);

        if (EditorGUI.EndChangeCheck())
        {
            config.SetProfileAssetPath(currentCategoria, entry.Definition.Nome,
                newProfile != null ? AssetDatabase.GetAssetPath(newProfile) : string.Empty);
        }

        GUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
        if (GUILayout.Button("Criar Perfil"))
        {
            string targetFolder = config.ResolveFolder(currentCategoria, entry.Definition.Nome, ExoAssetType.Prefabs);
            if (!string.IsNullOrEmpty(targetFolder))
            {
                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                string assetPath = (targetFolder + "/ExoPrefabProfile_" + entry.Definition.Nome + ".asset").Replace("\\", "/");

                ExoPrefabProfile existing = AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(assetPath);
                if (existing != null)
                {
                    Debug.LogWarning("[ExoConfig] Perfil ja existe em: " + assetPath);
                    EditorGUIUtility.PingObject(existing);
                }
                else
                {
                    ExoPrefabProfile newAsset = ScriptableObject.CreateInstance<ExoPrefabProfile>();
                    newAsset.entityType = currentCategoria == ExoCategory.Monstros ? ExoEntityType.Monstro
                                       : currentCategoria == ExoCategory.Environment ? ExoEntityType.Edificio
                                       : ExoEntityType.Personagem;

                    if (currentCategoria == ExoCategory.Monstros)
                    {
                        newAsset.gameObjectTag = "Enemy";
                        newAsset.gameObjectLayer = 7;
                    }
                    else if (currentCategoria == ExoCategory.Environment)
                    {
                        newAsset.gameObjectTag = "Untagged";
                        newAsset.gameObjectLayer = 0;
                    }

                    AssetDatabase.CreateAsset(newAsset, assetPath);
                    AssetDatabase.SaveAssets();
                    config.SetProfileAssetPath(currentCategoria, entry.Definition.Nome, assetPath);
                    EditorGUIUtility.PingObject(newAsset);
                    Debug.Log("[ExoConfig] Perfil criado em: " + assetPath);
                }
            }
            else
            {
                Debug.LogError("[ExoConfig] Nao foi possivel resolver a pasta de Prefabs da entidade antes de criar o perfil.");
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
}
