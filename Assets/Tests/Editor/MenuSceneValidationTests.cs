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
    private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    private const string SupportedLobbyScenePath = "Assets/Scenes/LobbyScene.unity";

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

    private static void WithMenuScene(System.Action<MonoBehaviour, Button, Button> assertion)
    {
        SceneSetup[] originalSetup = CaptureOriginalSceneSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            MonoBehaviour menuManager = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(component => component != null && component.GetType().Name == "MenuManager");
            Button singleplayerButton = GetAllButtons(scene).FirstOrDefault(button => button.gameObject.name == "Singleplayer");
            Button multiplayerButton = GetAllButtons(scene).FirstOrDefault(button => button.gameObject.name == "Multiplayer");

            Assert.That(menuManager, Is.Not.Null, "MenuManager nao encontrado na MenuScene.");
            Assert.That(singleplayerButton, Is.Not.Null, "Botao 'Singleplayer' nao encontrado na MenuScene.");
            Assert.That(multiplayerButton, Is.Not.Null, "Botao 'Multiplayer' nao encontrado na MenuScene.");

            assertion(menuManager, singleplayerButton, multiplayerButton);
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

    private static T[] GetAllComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
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
