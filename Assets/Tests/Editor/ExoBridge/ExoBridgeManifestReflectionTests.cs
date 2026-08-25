using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditMode tests deliberately use reflection because the production bridge
/// belongs to Assembly-CSharp-Editor while the existing test asmdef only
/// references the pure ExoConfig Core assembly. This still exercises the
/// public disk contract in the exact assembly Unity runs in the editor.
/// </summary>
public sealed class ExoBridgeManifestReflectionTests
{
    private string _assetDirectory;

    [SetUp]
    public void SetUp()
    {
        _assetDirectory = "Assets/ExoBridge/Incoming/Tests/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(ToFullPath(_assetDirectory));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(ToFullPath(_assetDirectory)))
            Directory.Delete(ToFullPath(_assetDirectory), true);
        AssetDatabase.Refresh();
    }

    [Test]
    public void ValidManifest_AcceptsHashesAxesScaleAndArchive()
    {
        string manifestPath = CreateManifest();
        object validation = InvokeReader("ReadAndValidate", manifestPath);

        Assert.That(GetProperty<bool>(validation, "IsValid"), Is.True);
    }

    [Test]
    public void PathTraversal_IsRejected()
    {
        string manifestPath = CreateManifest(modelPath: "../outside.fbx");
        object validation = InvokeReader("ReadAndValidate", manifestPath);

        Assert.That(GetProperty<bool>(validation, "IsValid"), Is.False);
        Assert.That(ErrorsText(validation), Does.Contain("Caminho inseguro"));
    }

    [Test]
    public void ChangedContentAfterManifest_IsRejectedByHash()
    {
        string manifestPath = CreateManifest();
        File.AppendAllText(ToFullPath(_assetDirectory + "/model/Test.fbx"), "tampered");
        object validation = InvokeReader("ReadAndValidate", manifestPath);

        Assert.That(GetProperty<bool>(validation, "IsValid"), Is.False);
        Assert.That(ErrorsText(validation), Does.Contain("SHA-256 nao confere"));
    }

    [Test]
    public void UnknownSchema_IsRejected()
    {
        string manifestPath = CreateManifest(schemaVersion: 99);
        object validation = InvokeReader("ReadAndValidate", manifestPath);

        Assert.That(GetProperty<bool>(validation, "IsValid"), Is.False);
        Assert.That(ErrorsText(validation), Does.Contain("schemaVersion"));
    }

    [Test]
    public void InspectionWithUnknownCategory_DoesNotWriteCanonicalConfig()
    {
        string configPath = "Assets/Editor/ExoConfig/ExoToolConfig.asset";
        byte[] before = File.ReadAllBytes(ToFullPath(configPath));
        string manifestPath = CreateManifest(category: "UnknownCategory");

        Type service = RequireType("ExoBridgeService");
        object inspection = service.GetMethod("Inspect", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { manifestPath });

        Assert.That(inspection, Is.Not.Null);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(ToFullPath(configPath)));
    }

    [Test]
    public void ProfileBindings_ResolveOnlyExactDeclaredSlotsAndActions()
    {
        Type profileType = RequireType("ExoPrefabProfile");
        Type materialBindingType = RequireType("ExoMaterialSlotBinding");
        Type animationBindingType = RequireType("ExoAnimationBinding");
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            object materialBinding = Activator.CreateInstance(materialBindingType);
            materialBindingType.GetField("sourceSlot").SetValue(materialBinding, "Cube[0]::Body");
            materialBindingType.GetField("rendererPath").SetValue(materialBinding, "Body");
            materialBindingType.GetField("rendererMaterialIndex").SetValue(materialBinding, 0);
            SetSingleBinding(profile, profileType, "materialSlotBindings", materialBindingType, materialBinding);

            object animationBinding = Activator.CreateInstance(animationBindingType);
            animationBindingType.GetField("actionName").SetValue(animationBinding, "Idle");
            SetSingleBinding(profile, profileType, "animationBindings", animationBindingType, animationBinding);

            Assert.That(profileType.GetMethod("FindMaterialBinding").Invoke(profile, new object[] { "Cube[0]::Body" }), Is.SameAs(materialBinding));
            Assert.That(profileType.GetMethod("FindMaterialBinding").Invoke(profile, new object[] { "cube[0]::body" }), Is.Null);
            Assert.That(profileType.GetMethod("FindAnimationBinding").Invoke(profile, new object[] { "Idle" }), Is.SameAs(animationBinding));
            Assert.That(profileType.GetMethod("FindAnimationBinding").Invoke(profile, new object[] { "Run" }), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    private string CreateManifest(int schemaVersion = 1, string category = "Personagens", string modelPath = "model/Test.fbx")
    {
        string archivePath = "source/Test.blend.zip";
        string modelFullPath = ToFullPath(_assetDirectory + "/" + modelPath);
        if (!modelPath.Contains(".."))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(modelFullPath));
            File.WriteAllText(modelFullPath, "fbx-content");
        }
        string archiveFullPath = ToFullPath(_assetDirectory + "/" + archivePath);
        Directory.CreateDirectory(Path.GetDirectoryName(archiveFullPath));
        using (ZipArchive archive = ZipFile.Open(archiveFullPath, ZipArchiveMode.Create))
        using (StreamWriter writer = new StreamWriter(archive.CreateEntry("Test.blend").Open()))
            writer.Write("blend-content");

        string modelHash = modelPath.Contains("..") ? new string('0', 64) : Sha256(modelFullPath);
        string json = "{\n"
            + "  \"schemaVersion\": " + schemaVersion + ",\n"
            + "  \"packageId\": \"123e4567-e89b-12d3-a456-426614174000\",\n"
            + "  \"exportedAtUtc\": \"2026-08-25T12:00:00Z\",\n"
            + "  \"entity\": { \"name\": \"BridgeTest\", \"category\": \"" + category + "\" },\n"
            + "  \"exporter\": { \"addonVersion\": \"1.0.0\", \"blenderVersion\": \"5.2.0\", \"sourceBlendFilename\": \"Test.blend\" },\n"
            + "  \"exportSettings\": { \"forwardAxis\": \"-Z\", \"upAxis\": \"Y\", \"globalScale\": 1.0, \"applyUnitScale\": true },\n"
            + "  \"files\": [\n"
            + "    { \"kind\": \"model\", \"relativePath\": \"" + modelPath + "\", \"sha256\": \"" + modelHash + "\" },\n"
            + "    { \"kind\": \"source_blend_archive\", \"relativePath\": \"" + archivePath + "\", \"sha256\": \"" + Sha256(archiveFullPath) + "\" }\n"
            + "  ], \"materials\": [], \"animations\": []\n"
            + "}";
        string manifestPath = _assetDirectory + "/exo-package.json";
        File.WriteAllText(ToFullPath(manifestPath), json);
        return manifestPath;
    }

    private static object InvokeReader(string method, string manifestPath)
    {
        Type reader = RequireType("ExoBridgeManifestReader");
        return reader.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { manifestPath, true });
    }

    private static Type RequireType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp-Editor");
        Assert.That(type, Is.Not.Null, typeName + " nao foi carregado no assembly de Editor.");
        return type;
    }

    private static T GetProperty<T>(object value, string property)
    {
        return (T)GetProperty(value, property);
    }

    private static object GetProperty(object value, string property)
    {
        return value.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance).GetValue(value);
    }

    private static string ErrorsText(object validation)
    {
        System.Collections.IEnumerable errors = (System.Collections.IEnumerable)GetProperty(validation, "Errors");
        string text = string.Empty;
        foreach (object error in errors) text += error + "\n";
        return text;
    }

    private static void SetSingleBinding(ScriptableObject profile, Type profileType, string fieldName, Type bindingType, object binding)
    {
        Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(bindingType);
        System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(listType);
        list.Add(binding);
        profileType.GetField(fieldName).SetValue(profile, list);
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath));
    }

    private static string Sha256(string path)
    {
        using (SHA256 hash = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
