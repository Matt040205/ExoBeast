using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectionAndEnemyScalingTests
{
    private const string SelectionScenePath = "Assets/aaPasta/Cenas/EscolherPersonagem.unity";
    private const string TeamSlotPrefabPath = "Assets/aaPasta/CoreScripts/Managers/Saves/slotPersonagemPrefab.prefab";
    private const string CommanderSlotPrefabPath = "Assets/aaPasta/CoreScripts/Managers/Saves/slotPersonagemPrefabComando.prefab";

    private static readonly Color[] ExpectedPlayerColors =
    {
        new Color(0.9764706f, 0.5921569f, 0.9803922f, 1f),
        new Color(0.5921569f, 0.8352941f, 0.9803922f, 1f),
        new Color(0.6980392f, 0.9803922f, 0.5921569f, 1f),
        new Color(0.9803922f, 0.9098039f, 0.5921569f, 1f),
    };

    [Test]
    public void EnemyMultiplayerScalingUsesExpectedHealthMultipliers()
    {
        MethodInfo getHealthMultiplier = GetRequiredMethod("EnemyMultiplayerScaling", "GetHealthMultiplier");
        MethodInfo applyHealthScaling = GetRequiredMethod("EnemyMultiplayerScaling", "ApplyHealthScaling");

        Assert.That((float)getHealthMultiplier.Invoke(null, new object[] { -1 }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)getHealthMultiplier.Invoke(null, new object[] { 0 }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)getHealthMultiplier.Invoke(null, new object[] { 1 }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)getHealthMultiplier.Invoke(null, new object[] { 2 }), Is.EqualTo(1.3f).Within(0.0001f));
        Assert.That((float)getHealthMultiplier.Invoke(null, new object[] { 3 }), Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That((float)getHealthMultiplier.Invoke(null, new object[] { 4 }), Is.EqualTo(1.7f).Within(0.0001f));
        Assert.That((float)getHealthMultiplier.Invoke(null, new object[] { 8 }), Is.EqualTo(1.7f).Within(0.0001f));

        Assert.That((float)applyHealthScaling.Invoke(null, new object[] { 100f, 3 }), Is.EqualTo(150f).Within(0.0001f));
    }

    [Test]
    public void EscolherPersonagemSceneHasExpectedPlayerColors()
    {
        WithSelectionScene(selectionManager =>
        {
            SerializedObject serializedObject = new SerializedObject(selectionManager);
            SerializedProperty colorsProperty = serializedObject.FindProperty("coresPorJogador");

            Assert.That(colorsProperty, Is.Not.Null, "Campo serializado 'coresPorJogador' nao encontrado.");
            Assert.That(colorsProperty.arraySize, Is.EqualTo(ExpectedPlayerColors.Length));

            for (int i = 0; i < ExpectedPlayerColors.Length; i++)
                AssertColor(colorsProperty.GetArrayElementAtIndex(i).colorValue, ExpectedPlayerColors[i], $"Player {i + 1}");
        });
    }

    [TestCase(TeamSlotPrefabPath)]
    [TestCase(CommanderSlotPrefabPath)]
    public void SlotPrefabsTintVisiblePlayerOverlays(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, $"Prefab nao encontrado: {prefabPath}");

        GameObject instance = Object.Instantiate(prefab);
        try
        {
            MonoBehaviour slot = instance.GetComponents<MonoBehaviour>()
                .FirstOrDefault(component => component != null && component.GetType().Name == "SlotEquipeUI");
            Assert.That(slot, Is.Not.Null, $"SlotEquipeUI ausente em {prefabPath}");

            Color expectedColor = ExpectedPlayerColors[1];
            MethodInfo defineColor = slot.GetType().GetMethod("DefinirCorDoJogador", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(defineColor, Is.Not.Null, "Metodo DefinirCorDoJogador nao encontrado.");
            defineColor.Invoke(slot, new object[] { expectedColor });

            Image[] overlays = instance.GetComponentsInChildren<Image>(true)
                .Where(image => image != null &&
                                (image.gameObject.name == "BordaOverlay" ||
                                 image.gameObject.name == "CoroaOverlay"))
                .ToArray();

            Assert.That(overlays.Length, Is.GreaterThan(0), $"Nenhum overlay colorivel em {prefabPath}");
            foreach (Image overlay in overlays)
                AssertColor(overlay.color, expectedColor, overlay.gameObject.name);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void WithSelectionScene(System.Action<MonoBehaviour> assertion)
    {
        SceneSetup[] originalSetup = CaptureOriginalSceneSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(SelectionScenePath, OpenSceneMode.Single);
            MonoBehaviour selectionManager = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(component => component != null && component.GetType().Name == "SelecaoManager");

            Assert.That(selectionManager, Is.Not.Null, "SelecaoManager nao encontrado em EscolherPersonagem.");
            assertion(selectionManager);
        }
        finally
        {
            RestoreOriginalSceneSetup(originalSetup);
        }
    }

    private static void AssertColor(Color actual, Color expected, string label)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), $"{label} r");
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), $"{label} g");
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), $"{label} b");
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), $"{label} a");
    }

    private static MethodInfo GetRequiredMethod(string typeName, string methodName)
    {
        System.Type type = System.Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Tipo {typeName} nao encontrado em Assembly-CSharp.");

        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, $"Metodo {typeName}.{methodName} nao encontrado.");
        return method;
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
