using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ExoBeasts.Editor
{
    [InitializeOnLoad]
    public static class BuildSceneListGuard
    {
        public static readonly string[] CanonicalScenePaths =
        {
            "Assets/Cenas/NetworkBootstrap.unity",
            "Assets/Cenas/MenuScene.unity",
            "Assets/Cenas/EscolherPersonagem.unity",
            "Assets/Cenas/CenaSeleçao.unity",
            "Assets/Cenas/LobbyScene.unity",
            "Assets/Cenas/Rastros.unity",
            "Assets/Cenas/Lose.unity",
            "Assets/Cenas/Win.unity",
            "Assets/Cenas/CenaMapaNOVO.unity"
        };

        static BuildSceneListGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/ExoBeasts/Repair Build Scene List")]
        public static void RepairFromMenu()
        {
            if (TryEnsureCanonicalScenes(autoRepair: true, out string error))
            {
                EditorUtility.DisplayDialog(
                    "ExoBeasts Build Scene List",
                    "Build Scene List reparada com sucesso.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "ExoBeasts Build Scene List - Erro",
                error,
                "OK");
        }

        public static bool TryEnsureCanonicalScenes(bool autoRepair, out string error)
        {
            if (!TryValidateCanonicalSceneAssets(out error))
            {
                return false;
            }

            bool repaired = false;

            if (!SceneListMatches(EditorBuildSettings.globalScenes))
            {
                if (!autoRepair)
                {
                    error = BuildMismatchMessage("EditorBuildSettings.globalScenes", EditorBuildSettings.globalScenes);
                    return false;
                }

                EditorBuildSettings.globalScenes = CreateCanonicalSceneList();
                repaired = true;
            }

            if (!SceneListMatches(EditorBuildSettings.scenes))
            {
                if (!autoRepair)
                {
                    error = BuildMismatchMessage("EditorBuildSettings.scenes", EditorBuildSettings.scenes);
                    return false;
                }

                EditorBuildSettings.scenes = CreateCanonicalSceneList();
                repaired = true;
            }

            if (repaired)
            {
                AssetDatabase.SaveAssets();
                // BUG FIX (2026-05-21): MPPM clone usa cache de Library/ separada e pode nao re-ler
                // ProjectSettings/EditorBuildSettings.asset apos SaveAssets. Sem este Refresh,
                // o clone entra em Play Mode com lista de cenas dessincronizada e NGO SceneManager
                // falha ao resolver "EscolherPersonagem" quando host chama LoadScene durante a partida.
                // Sintoma: cliente MPPM nao acompanha host na transicao LobbyScene -> EscolherPersonagem.
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Debug.Log("[BuildSceneListGuard] Build Scene List reparada para a lista canonica do projeto.");
            }

            if (!SceneListMatches(EditorBuildSettings.globalScenes))
            {
                error = BuildMismatchMessage("EditorBuildSettings.globalScenes", EditorBuildSettings.globalScenes);
                return false;
            }

            if (!SceneListMatches(EditorBuildSettings.scenes))
            {
                error = BuildMismatchMessage("EditorBuildSettings.scenes", EditorBuildSettings.scenes);
                return false;
            }

            error = null;
            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // EnteredPlayMode: diagnostico apenas. Em MPPM, este hook dispara no clone
            // QUANDO o clone entra em Play Mode. Se a lista canonica nao bater aqui, o clone
            // recebeu uma view diferente do projeto original e o NGO SceneManager pode falhar
            // ao tentar carregar cenas via nome durante a partida.
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SceneListMatches(EditorBuildSettings.scenes) ||
                    !SceneListMatches(EditorBuildSettings.globalScenes))
                {
                    Debug.LogError(
                        "[BuildSceneListGuard] DRIFT DETECTADO em EnteredPlayMode. " +
                        "Este processo (provavelmente um clone MPPM) tem lista de cenas " +
                        "dessincronizada da canonica. NGO SceneManager pode falhar ao carregar " +
                        "cenas via nome. Esperado:\n" + FormatSceneList(CreateCanonicalSceneList()) +
                        "\nAtual (scenes):\n" + FormatSceneList(EditorBuildSettings.scenes) +
                        "\nAtual (globalScenes):\n" + FormatSceneList(EditorBuildSettings.globalScenes));
                }
                return;
            }

            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (TryEnsureCanonicalScenes(autoRepair: true, out string error))
            {
                return;
            }

            Debug.LogError($"[BuildSceneListGuard] Play Mode bloqueado: {error}");
            EditorApplication.isPlaying = false;
        }

        private static bool TryValidateCanonicalSceneAssets(out string error)
        {
            List<string> missingScenes = new List<string>();

            foreach (string scenePath in CanonicalScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    missingScenes.Add(scenePath);
                }
            }

            if (missingScenes.Count == 0)
            {
                error = null;
                return true;
            }

            error = "Cenas canonicas ausentes:\n" + string.Join("\n", missingScenes);
            return false;
        }

        private static EditorBuildSettingsScene[] CreateCanonicalSceneList()
        {
            return CanonicalScenePaths
                .Select(scenePath => new EditorBuildSettingsScene(scenePath, enabled: true))
                .ToArray();
        }

        private static bool SceneListMatches(EditorBuildSettingsScene[] scenes)
        {
            if (scenes == null || scenes.Length != CanonicalScenePaths.Length)
            {
                return false;
            }

            for (int i = 0; i < CanonicalScenePaths.Length; i++)
            {
                if (!scenes[i].enabled || scenes[i].path != CanonicalScenePaths[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildMismatchMessage(string listName, EditorBuildSettingsScene[] actualScenes)
        {
            return $"{listName} nao corresponde a lista canonica.\n" +
                   "Esperado:\n" +
                   FormatSceneList(CreateCanonicalSceneList()) +
                   "\nAtual:\n" +
                   FormatSceneList(actualScenes);
        }

        private static string FormatSceneList(EditorBuildSettingsScene[] scenes)
        {
            if (scenes == null || scenes.Length == 0)
            {
                return "  (vazia)";
            }

            return string.Join(
                "\n",
                scenes.Select((scene, index) =>
                    $"  {index}: {(scene.enabled ? "[x]" : "[ ]")} {scene.path}"));
        }
    }
}
