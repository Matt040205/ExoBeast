using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuSceneValidationTests
{
    private const string NetworkBootstrapScenePath = "Assets/Cenas/NetworkBootstrap.unity";
    private const string MenuScenePath = "Assets/Cenas/MenuScene.unity";
    private const string SupportedLobbyScenePath = "Assets/Cenas/LobbyScene.unity";
    private static readonly string[] CanonicalScenePaths =
    {
        "Assets/Cenas/NetworkBootstrap.unity",
        "Assets/Cenas/MenuScene.unity",
        "Assets/Cenas/CenaSeleçao.unity",
        SupportedLobbyScenePath,
        "Assets/Cenas/Rastros.unity",
        "Assets/Cenas/Lose.unity",
        "Assets/Cenas/Win.unity",
        "Assets/Cenas/CenaMapaNOVO.unity"
    };

    [Test]
    public void MenuManagerHasSerializedMainMenuButtons()
    {
        WithMenuScene((menuManager, singleplayerButton, multiplayerButton) =>
        {
            SerializedObject serializedObject = new SerializedObject(menuManager);
            SerializedProperty soloButtonProperty = serializedObject.FindProperty("botaoJogarSolo");
            SerializedProperty onlineButtonProperty = serializedObject.FindProperty("botaoJogarOnline");

            Assert.That(soloButtonProperty, Is.Not.Null, "Campo serializado 'botaoJogarSolo' nao encontrado.");
            Assert.That(onlineButtonProperty, Is.Not.Null, "Campo serializado 'botaoJogarOnline' nao encontrado.");

            Assert.That(soloButtonProperty.objectReferenceValue, Is.EqualTo(singleplayerButton));
            Assert.That(onlineButtonProperty.objectReferenceValue, Is.EqualTo(multiplayerButton));
        });
    }

    [Test]
    public void MenuSceneButtonsDoNotKeepLegacyPersistentListeners()
    {
        WithMenuScene((_, singleplayerButton, multiplayerButton) =>
        {
            Assert.That(singleplayerButton.onClick.GetPersistentEventCount(), Is.EqualTo(0),
                "Singleplayer nao pode manter listeners persistentes no YAML.");
            Assert.That(multiplayerButton.onClick.GetPersistentEventCount(), Is.EqualTo(0),
                "Multiplayer nao pode manter listeners persistentes no YAML.");

            foreach (Button button in GetAllButtons(SceneManager.GetActiveScene()))
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    string method = button.onClick.GetPersistentMethodName(i);
                    Assert.That(method, Is.Not.EqualTo("HostGame"));
                    Assert.That(method, Is.Not.EqualTo("AbrirPainelMultiplayer"));
                }
            }
        });
    }

    [Test]
    public void OnlyTheSupportedLobbySceneKeepsTheCanonicalFilename()
    {
        string[] lobbyScenes = Directory.GetFiles(Application.dataPath, "LobbyScene.unity", SearchOption.AllDirectories);

        Assert.That(lobbyScenes.Length, Is.EqualTo(1),
            "Deve existir apenas um arquivo chamado LobbyScene.unity no projeto.");

        string normalizedPath = lobbyScenes[0].Replace('\\', '/');
        string expectedPath = Path.Combine(Directory.GetCurrentDirectory(), SupportedLobbyScenePath).Replace('\\', '/');
        Assert.That(normalizedPath, Is.EqualTo(expectedPath));
    }

    [Test]
    public void CanonicalScenesAreEnabledAndOrderedInBuildSettings()
    {
        AssertCanonicalSceneList(EditorBuildSettings.globalScenes, "EditorBuildSettings.globalScenes");
        AssertCanonicalSceneList(EditorBuildSettings.scenes, "EditorBuildSettings.scenes");
    }

    [Test]
    public void NetworkBootstrapSceneLoadsMenuScene()
    {
        WithScene(NetworkBootstrapScenePath, scene =>
        {
            MonoBehaviour bootstrapper = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(component => component != null && component.GetType().Name == "SceneBootstrapper");

            Assert.That(bootstrapper, Is.Not.Null, "SceneBootstrapper nao encontrado na NetworkBootstrap.");

            SerializedObject serializedObject = new SerializedObject(bootstrapper);
            SerializedProperty initialSceneName = serializedObject.FindProperty("initialSceneName");
            Assert.That(initialSceneName, Is.Not.Null, "Campo initialSceneName nao encontrado no SceneBootstrapper.");
            Assert.That(initialSceneName.stringValue, Is.EqualTo("MenuScene"));
        });
    }

    [Test]
    public void LobbySceneResolvesToBuildIndex()
    {
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(SupportedLobbyScenePath);

        Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0),
            "LobbyScene precisa resolver para build index no Play Mode e nos clones MPPM.");
    }

    [Test]
    public void LobbySceneJoinButtonsDoNotLetChildTextStealRaycasts()
    {
        WithScene(SupportedLobbyScenePath, scene =>
        {
            string[] guardedButtonNames = { "EntrarLobbyTransferência", "LobbyPublico" };

            foreach (string buttonName in guardedButtonNames)
            {
                Button button = GetAllButtons(scene).FirstOrDefault(candidate => candidate.gameObject.name == buttonName);
                Assert.That(button, Is.Not.Null, $"Botao '{buttonName}' nao encontrado na LobbyScene.");

                string[] raycastLabels = button.GetComponentsInChildren<Graphic>(true)
                    .Where(IsTextGraphic)
                    .Where(graphic => graphic.raycastTarget)
                    .Select(graphic => GetHierarchyPath(graphic.transform))
                    .ToArray();

                Assert.That(raycastLabels, Is.Empty,
                    $"Textos filhos de '{buttonName}' nao podem receber raycast, pois expandem a area clicavel do botao: {string.Join(", ", raycastLabels)}");
            }
        });
    }

    private static void WithMenuScene(System.Action<MonoBehaviour, Button, Button> assertion)
    {
        WithScene(MenuScenePath, scene =>
        {
            MonoBehaviour menuManager = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(component => component != null && component.GetType().Name == "MenuManager");
            Button singleplayerButton = GetAllButtons(scene).FirstOrDefault(button => button.gameObject.name == "Singleplayer");
            Button multiplayerButton = GetAllButtons(scene).FirstOrDefault(button => button.gameObject.name == "Multiplayer");

            Assert.That(menuManager, Is.Not.Null, "MenuManager nao encontrado na MenuScene.");
            Assert.That(singleplayerButton, Is.Not.Null, "Botao 'Singleplayer' nao encontrado na MenuScene.");
            Assert.That(multiplayerButton, Is.Not.Null, "Botao 'Multiplayer' nao encontrado na MenuScene.");

            assertion(menuManager, singleplayerButton, multiplayerButton);
        });
    }

    private static void WithScene(string scenePath, System.Action<Scene> assertion)
    {
        SceneSetup[] originalSetup = CaptureOriginalSceneSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            assertion(scene);
        }
        finally
        {
            RestoreOriginalSceneSetup(originalSetup);
        }
    }

    private static Button[] GetAllButtons(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Button>(true))
            .ToArray();
    }

    private static void AssertCanonicalSceneList(EditorBuildSettingsScene[] scenes, string listName)
    {
        Assert.That(scenes, Is.Not.Null, $"{listName} nao pode ser null.");
        Assert.That(scenes.Length, Is.EqualTo(CanonicalScenePaths.Length),
            $"{listName} precisa ter exatamente a lista canonica de cenas.");

        for (int i = 0; i < CanonicalScenePaths.Length; i++)
        {
            Assert.That(scenes[i].enabled, Is.True, $"{listName}[{i}] precisa estar habilitada.");
            Assert.That(scenes[i].path, Is.EqualTo(CanonicalScenePaths[i]), $"{listName}[{i}] fora da ordem canonica.");
        }
    }

    private static T[] GetAllComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static bool IsTextGraphic(Graphic graphic)
    {
        string typeName = graphic.GetType().FullName;
        return typeName == "TMPro.TextMeshProUGUI" || typeName == "UnityEngine.UI.Text";
    }

    private static string GetHierarchyPath(Transform transform)
    {
        return string.Join("/", transform.GetComponentsInParent<Transform>(true)
            .Reverse()
            .Select(parent => parent.gameObject.name));
    }

    private static void RestoreOriginalSceneSetup(SceneSetup[] originalSetup)
    {
        if (originalSetup != null && originalSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static SceneSetup[] CaptureOriginalSceneSetup()
    {
        try
        {
            return EditorSceneManager.GetSceneManagerSetup();
        }
        catch (System.ArgumentException)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return new SceneSetup[0];
        }
    }
}
