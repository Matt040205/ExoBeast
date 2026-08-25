using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ExoBeasts.ExoConfig.Core;

/// <summary>Superficie de revisao humana; nunca importa automaticamente.</summary>
public sealed class ExoBridgeWindow : EditorWindow
{
    private List<string> _manifestPaths = new List<string>();
    private ExoBridgeInspection _inspection;
    private Vector2 _scroll;

    [MenuItem("Exo Bridge/Pacotes", false, 1000)]
    public static void ShowWindow()
    {
        GetWindow<ExoBridgeWindow>("Exo Bridge");
    }

    private void OnEnable()
    {
        RefreshPackages();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Exo Bridge - Pacotes Blender", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Pacotes em Incoming sao apenas evidencias. Use Previa para validar hash, schema, perfil Unity e referencias antes de qualquer promocao.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Atualizar pacotes")) RefreshPackages();
            if (GUILayout.Button("Configurar perfis")) ExoBridgeSetupWindow.ShowWindow();
        }

        if (_manifestPaths.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum exo-package.json foi encontrado em Assets/ExoBridge/Incoming.", MessageType.None);
            return;
        }

        foreach (string manifestPath in _manifestPaths)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(manifestPath, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Previa", GUILayout.Width(70)))
                    _inspection = ExoBridgeService.Inspect(manifestPath);
            }
        }

        if (_inspection == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Resultado da previa", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(220));
        foreach (ExoBuildMessage message in _inspection.Report.Messages)
        {
            MessageType type = message.Severity == ExoBuildMessageSeverity.Error ? MessageType.Error
                : message.Severity == ExoBuildMessageSeverity.Warning ? MessageType.Warning
                : MessageType.Info;
            EditorGUILayout.HelpBox(message.ToString(), type);
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(!_inspection.IsReady))
        {
            if (GUILayout.Button("Importar pacote aprovado", GUILayout.Height(30)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Promover pacote Exo Bridge",
                    "O pacote ja passou na previa. A promocao copiara arquivos para os caminhos canonicos, criara backup local de arquivos substituidos e atualizara somente os bindings explicitamente configurados. Continuar?",
                    "Promover",
                    "Cancelar");
                if (confirmed)
                {
                    ExoBuildReport report = ExoBridgeService.ImportApprovedPackage(_inspection.ManifestValidation.ManifestPath);
                    _inspection = ExoBridgeService.Inspect(_inspection.ManifestValidation.ManifestPath);
                    foreach (ExoBuildMessage message in report.Messages)
                        Debug.Log("[ExoBridge] " + message);
                }
            }
        }
    }

    private void RefreshPackages()
    {
        _manifestPaths = ExoBridgeService.FindManifestPaths();
        _inspection = null;
    }
}
