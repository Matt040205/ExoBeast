using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEngine;

/// <summary>
/// Contrato de disco entre o addon Blender Exo Bridge e a extensao Unity.
/// O formato deliberadamente usa apenas arrays e campos simples para que
/// JsonUtility possa le-lo sem uma dependencia transitiva de pacote.
/// </summary>
[Serializable]
public sealed class ExoBridgeManifest
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion;
    public string packageId;
    public string exportedAtUtc;
    public ExoBridgeEntity entity = new ExoBridgeEntity();
    public ExoBridgeExporter exporter = new ExoBridgeExporter();
    public ExoBridgeExportSettings exportSettings = new ExoBridgeExportSettings();
    public ExoBridgeFileEntry[] files = Array.Empty<ExoBridgeFileEntry>();
    public ExoBridgeMaterialEntry[] materials = Array.Empty<ExoBridgeMaterialEntry>();
    public ExoBridgeAnimationEntry[] animations = Array.Empty<ExoBridgeAnimationEntry>();

    public ExoBridgeFileEntry FindSingleFile(string kind)
    {
        ExoBridgeFileEntry found = null;
        foreach (ExoBridgeFileEntry entry in files ?? Array.Empty<ExoBridgeFileEntry>())
        {
            if (entry == null || !string.Equals(entry.kind, kind, StringComparison.Ordinal))
                continue;

            if (found != null)
                return null;

            found = entry;
        }

        return found;
    }

    public ExoBridgeFileEntry FindFile(string relativePath)
    {
        foreach (ExoBridgeFileEntry entry in files ?? Array.Empty<ExoBridgeFileEntry>())
        {
            if (entry != null && string.Equals(entry.relativePath, relativePath, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }
}

[Serializable]
public sealed class ExoBridgeEntity
{
    public string name;
    public string category;
}

[Serializable]
public sealed class ExoBridgeExporter
{
    public string addonVersion;
    public string blenderVersion;
    public string sourceBlendFilename;
}

[Serializable]
public sealed class ExoBridgeExportSettings
{
    public string forwardAxis;
    public string upAxis;
    public float globalScale;
    public bool applyUnitScale;
}

[Serializable]
public sealed class ExoBridgeFileEntry
{
    // model, texture, animation ou source_blend_archive
    public string kind;
    public string relativePath;
    public string sha256;
}

[Serializable]
public sealed class ExoBridgeMaterialEntry
{
    public string slotName;
    public string baseTexturePath;
    public string shadingTexturePath;
}

[Serializable]
public sealed class ExoBridgeAnimationEntry
{
    public string actionName;
    public string filePath;
}

/// <summary>Resultado da leitura segura de um pacote; nao altera assets.</summary>
public sealed class ExoBridgeManifestValidation
{
    public ExoBridgeManifest Manifest { get; internal set; }
    public string ManifestPath { get; internal set; }
    public string PackageRoot { get; internal set; }
    public List<string> Errors { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();
    public bool IsValid => Manifest != null && Errors.Count == 0;

    public void Error(string message) => Errors.Add(message);
    public void Warning(string message) => Warnings.Add(message);
}

public static class ExoBridgeManifestReader
{
    public const string IncomingRoot = "Assets/ExoBridge/Incoming";
    public const string ManifestFileName = "exo-package.json";

    public static ExoBridgeManifestValidation ReadAndValidate(string manifestPath, bool verifyHashes = true)
    {
        ExoBridgeManifestValidation result = new ExoBridgeManifestValidation
        {
            ManifestPath = NormalizeProjectPath(manifestPath),
            PackageRoot = NormalizeProjectPath(Path.GetDirectoryName(manifestPath))
        };

        if (!IsProjectPathInside(result.ManifestPath, IncomingRoot))
        {
            result.Error("O manifesto precisa estar dentro de \"" + IncomingRoot + "\".");
            return result;
        }

        string manifestFullPath = ToFullPath(result.ManifestPath);
        if (!File.Exists(manifestFullPath))
        {
            result.Error("Manifesto nao encontrado: \"" + result.ManifestPath + "\".");
            return result;
        }

        try
        {
            result.Manifest = JsonUtility.FromJson<ExoBridgeManifest>(File.ReadAllText(manifestFullPath));
        }
        catch (Exception exception)
        {
            result.Error("Manifesto JSON invalido: " + exception.Message);
            return result;
        }

        ValidateManifest(result, verifyHashes);
        return result;
    }

    public static string ToFullPath(string projectPath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.GetFullPath(Path.Combine(projectRoot, projectPath ?? string.Empty));
    }

    public static string NormalizeProjectPath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    public static bool IsSafeRelativePath(string packageRoot, string relativePath, out string fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            return false;

        string root = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;

        fullPath = candidate;
        return true;
    }

    private static void ValidateManifest(ExoBridgeManifestValidation result, bool verifyHashes)
    {
        ExoBridgeManifest manifest = result.Manifest;
        if (manifest == null)
        {
            result.Error("Manifesto vazio.");
            return;
        }

        if (manifest.schemaVersion != ExoBridgeManifest.CurrentSchemaVersion)
            result.Error("schemaVersion \"" + manifest.schemaVersion + "\" nao e suportada.");
        if (!Guid.TryParse(manifest.packageId, out _))
            result.Error("packageId deve ser um UUID valido.");
        if (!DateTime.TryParse(manifest.exportedAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out _))
            result.Error("exportedAtUtc deve ser uma data UTC valida.");
        if (manifest.entity == null || string.IsNullOrWhiteSpace(manifest.entity.name) || string.IsNullOrWhiteSpace(manifest.entity.category))
            result.Error("entity.name e entity.category sao obrigatorios.");
        if (manifest.exporter == null || string.IsNullOrWhiteSpace(manifest.exporter.addonVersion)
            || string.IsNullOrWhiteSpace(manifest.exporter.blenderVersion)
            || string.IsNullOrWhiteSpace(manifest.exporter.sourceBlendFilename))
            result.Error("A versao do addon, do Blender e a proveniencia do .blend sao obrigatorias.");

        if (manifest.exportSettings == null
            || !string.Equals(manifest.exportSettings.forwardAxis, "-Z", StringComparison.Ordinal)
            || !string.Equals(manifest.exportSettings.upAxis, "Y", StringComparison.Ordinal)
            || Math.Abs(manifest.exportSettings.globalScale - 1f) > 0.0001f
            || !manifest.exportSettings.applyUnitScale)
        {
            result.Error("O pacote precisa usar forward=-Z, up=Y, globalScale=1 e applyUnitScale=true.");
        }

        ValidateFiles(result, verifyHashes);
        ValidateMaterials(result);
        ValidateAnimations(result);
    }

    private static void ValidateFiles(ExoBridgeManifestValidation result, bool verifyHashes)
    {
        ExoBridgeManifest manifest = result.Manifest;
        HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
        int modelCount = 0;
        int sourceBlendArchiveCount = 0;

        foreach (ExoBridgeFileEntry file in manifest.files ?? Array.Empty<ExoBridgeFileEntry>())
        {
            if (file == null || string.IsNullOrWhiteSpace(file.kind) || string.IsNullOrWhiteSpace(file.relativePath))
            {
                result.Error("Cada arquivo precisa de kind e relativePath.");
                continue;
            }

            if (!paths.Add(file.relativePath))
                result.Error("Arquivo repetido no manifesto: \"" + file.relativePath + "\".");
            if (!IsSafeRelativePath(result.PackageRoot, file.relativePath, out string fullPath))
            {
                result.Error("Caminho inseguro no manifesto: \"" + file.relativePath + "\".");
                continue;
            }
            if (!File.Exists(fullPath))
            {
                result.Error("Arquivo declarado nao existe: \"" + file.relativePath + "\".");
                continue;
            }
            if (!IsSupportedFileKind(file.kind, file.relativePath))
                result.Error("Tipo de arquivo nao suportado para \"" + file.kind + "\": \"" + file.relativePath + "\".");
            else if (string.Equals(file.kind, "source_blend_archive", StringComparison.Ordinal))
                ValidateBlendArchive(result, fullPath, file.relativePath);
            if (!IsSha256(file.sha256))
                result.Error("SHA-256 invalido para \"" + file.relativePath + "\".");
            else if (verifyHashes && !string.Equals(ComputeSha256(fullPath), file.sha256, StringComparison.OrdinalIgnoreCase))
                result.Error("SHA-256 nao confere para \"" + file.relativePath + "\".");

            if (string.Equals(file.kind, "model", StringComparison.Ordinal)) modelCount++;
            if (string.Equals(file.kind, "source_blend_archive", StringComparison.Ordinal)) sourceBlendArchiveCount++;
        }

        if (modelCount != 1) result.Error("O pacote deve declarar exatamente um arquivo kind=model.");
        if (sourceBlendArchiveCount != 1) result.Error("O pacote deve declarar exatamente um arquivo kind=source_blend_archive.");
    }

    private static void ValidateBlendArchive(ExoBridgeManifestValidation result, string fullPath, string relativePath)
    {
        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(fullPath))
            {
                bool hasBlend = false;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (Path.GetExtension(entry.FullName).Equals(".blend", StringComparison.OrdinalIgnoreCase))
                    {
                        hasBlend = true;
                        break;
                    }
                }
                if (!hasBlend)
                    result.Error("Arquivo source_blend_archive nao contem um .blend: \"" + relativePath + "\".");
            }
        }
        catch (Exception exception)
        {
            result.Error("Arquivo source_blend_archive invalido: \"" + relativePath + "\" (" + exception.Message + ").");
        }
    }

    private static void ValidateMaterials(ExoBridgeManifestValidation result)
    {
        HashSet<string> slots = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExoBridgeMaterialEntry material in result.Manifest.materials ?? Array.Empty<ExoBridgeMaterialEntry>())
        {
            if (material == null || string.IsNullOrWhiteSpace(material.slotName) || string.IsNullOrWhiteSpace(material.baseTexturePath))
            {
                result.Error("Cada material precisa de slotName e baseTexturePath.");
                continue;
            }
            if (!slots.Add(material.slotName)) result.Error("Slot de material repetido: \"" + material.slotName + "\".");
            ValidateTextureReference(result, material.baseTexturePath, material.slotName);
            if (!string.IsNullOrWhiteSpace(material.shadingTexturePath))
                ValidateTextureReference(result, material.shadingTexturePath, material.slotName);
        }
    }

    private static void ValidateAnimations(ExoBridgeManifestValidation result)
    {
        HashSet<string> actions = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExoBridgeAnimationEntry animation in result.Manifest.animations ?? Array.Empty<ExoBridgeAnimationEntry>())
        {
            if (animation == null || string.IsNullOrWhiteSpace(animation.actionName) || string.IsNullOrWhiteSpace(animation.filePath))
            {
                result.Error("Cada animacao precisa de actionName e filePath.");
                continue;
            }
            if (!actions.Add(animation.actionName)) result.Error("Action repetida: \"" + animation.actionName + "\".");
            ExoBridgeFileEntry file = result.Manifest.FindFile(animation.filePath);
            if (file == null || !string.Equals(file.kind, "animation", StringComparison.Ordinal))
                result.Error("Action \"" + animation.actionName + "\" nao aponta para um arquivo kind=animation.");
        }
    }

    private static void ValidateTextureReference(ExoBridgeManifestValidation result, string path, string slot)
    {
        ExoBridgeFileEntry file = result.Manifest.FindFile(path);
        if (file == null || !string.Equals(file.kind, "texture", StringComparison.Ordinal))
            result.Error("Slot \"" + slot + "\" aponta para textura nao declarada: \"" + path + "\".");
    }

    private static bool IsSupportedFileKind(string kind, string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        switch (kind)
        {
            case "model": return extension == ".fbx";
            case "texture": return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga";
            case "animation": return extension == ".fbx";
            case "source_blend_archive": return extension == ".zip";
            default: return false;
        }
    }

    private static bool IsSha256(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
        foreach (char character in value)
        {
            if (!((character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F')))
                return false;
        }
        return true;
    }

    private static string ComputeSha256(string fullPath)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(fullPath))
        {
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static bool IsProjectPathInside(string candidate, string root)
    {
        string normalizedCandidate = NormalizeProjectPath(candidate).TrimEnd('/');
        string normalizedRoot = NormalizeProjectPath(root).TrimEnd('/');
        return normalizedCandidate.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
