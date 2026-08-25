using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Netcode.Components;
using ExoBeasts.ExoConfig.Core;

/// <summary>Previa imutavel de um pacote Exo Bridge.</summary>
public sealed class ExoBridgeInspection
{
    public ExoBridgeManifestValidation ManifestValidation { get; internal set; }
    public ExoBuildReport Report { get; } = new ExoBuildReport();
    public ExoCategory Category { get; internal set; }
    public ExoToolConfig Config { get; internal set; }
    public ExoToolConfigEntry Entry { get; internal set; }
    public ExoPrefabProfile Profile { get; internal set; }
    public string ModelAssetPath { get; internal set; }
    public IReadOnlyList<string> ExistingPrefabPaths { get; internal set; } = Array.Empty<string>();
    public bool IsReady => ManifestValidation != null && ManifestValidation.IsValid && !Report.HasErrors;
}

/// <summary>
/// Entrada segura para a ponte. Preview nao escreve nada. Import somente e
/// chamado pela janela depois de o usuario confirmar a previa sem erros.
/// </summary>
public static class ExoBridgeService
{
    public const string BackupRoot = "Assets/ExoBridge/Backups";

    public static List<string> FindManifestPaths()
    {
        string incomingFullPath = ExoBridgeManifestReader.ToFullPath(ExoBridgeManifestReader.IncomingRoot);
        if (!Directory.Exists(incomingFullPath)) return new List<string>();

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Directory.GetFiles(incomingFullPath, ExoBridgeManifestReader.ManifestFileName, SearchOption.AllDirectories)
            .Select(path => ExoBridgeManifestReader.NormalizeProjectPath(Path.GetRelativePath(projectRoot, path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ExoBridgeInspection Inspect(string manifestPath)
    {
        ExoBridgeInspection inspection = new ExoBridgeInspection
        {
            ManifestValidation = ExoBridgeManifestReader.ReadAndValidate(manifestPath)
        };

        foreach (string error in inspection.ManifestValidation.Errors)
            inspection.Report.Error(error, manifestPath);
        foreach (string warning in inspection.ManifestValidation.Warnings)
            inspection.Report.Warning(warning, manifestPath);

        if (!inspection.ManifestValidation.IsValid)
            return inspection;

        ExoBridgeManifest manifest = inspection.ManifestValidation.Manifest;
        if (!ExoCategoryParser.TryParse(manifest.entity.category, out ExoCategory category))
        {
            inspection.Report.Error("Categoria do manifesto nao e suportada: \"" + manifest.entity.category + "\".", manifest.entity.name);
            return inspection;
        }

        inspection.Category = category;
        inspection.Config = ExoToolConfig.Load();
        inspection.Entry = inspection.Config?.FindEntry(category, manifest.entity.name);
        if (inspection.Entry == null)
        {
            inspection.Report.Error("A entidade nao esta cadastrada no ExoToolConfig. Use Exo Bridge > Configurar perfis primeiro.", manifest.entity.name);
            return inspection;
        }

        if (string.IsNullOrWhiteSpace(inspection.Entry.ProfileAssetPath))
        {
            inspection.Report.Error("A entidade nao possui ExoPrefabProfile. Use Exo Bridge > Configurar perfis antes de importar.", manifest.entity.name);
            return inspection;
        }

        inspection.Profile = AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(inspection.Entry.ProfileAssetPath);
        if (inspection.Profile == null)
        {
            inspection.Report.Error("O ExoPrefabProfile vinculado nao pode ser carregado: \"" + inspection.Entry.ProfileAssetPath + "\".", manifest.entity.name);
            return inspection;
        }

        ValidateProfile(inspection);
        inspection.ModelAssetPath = GetPackageAssetPath(inspection, manifest.FindSingleFile("model"));
        inspection.ExistingPrefabPaths = ResolvePrefabPaths(inspection).Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray();

        ValidatePackageMaterialsAndAnimations(inspection);
        ValidatePackageAssetsCanBeImported(inspection);
        ValidateExistingState(inspection);

        // Exercita a configuracao do pipeline em modo somente-leitura antes
        // de promover qualquer arquivo. A ponte nunca depende de Selection.
        if (!inspection.Report.HasErrors)
        {
            ExoBuildReport pipelinePreview = ExoPrefabMenu.RunPipelineForPromotedAsset(
                manifest.entity.category,
                manifest.entity.name,
                GetExpectedPromotedModelPath(inspection),
                dryRun: true);
            CopyMessages(pipelinePreview, inspection.Report);
        }

        if (!inspection.Report.HasErrors)
            inspection.Report.Info("Previa aprovada: nenhuma alteracao foi aplicada. Confirme a importacao para promover o pacote.", manifest.entity.name);

        return inspection;
    }

    public static ExoBuildReport ImportApprovedPackage(string manifestPath)
    {
        ExoBridgeInspection inspection = Inspect(manifestPath);
        if (!inspection.IsReady)
            return inspection.Report;

        try
        {
            Dictionary<string, string> promotedPaths = PromotePackage(inspection);
            AssetDatabase.Refresh();

            bool hasExistingPrefab = inspection.ExistingPrefabPaths.Any(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null);
            if (hasExistingPrefab)
            {
                Dictionary<string, Material> materials = BuildOrUpdateMaterials(inspection, promotedPaths);
                if (inspection.Report.HasErrors) return inspection.Report;

                ExoBridgePrefabUpdater.UpdateExistingPrefabs(
                    inspection,
                    inspection.ExistingPrefabPaths,
                    inspection.ModelAssetPath,
                    materials);
            }
            else
            {
                ExoBuildReport pipelineReport = ExoPrefabMenu.RunPipelineForPromotedAsset(
                    inspection.ManifestValidation.Manifest.entity.category,
                    inspection.ManifestValidation.Manifest.entity.name,
                    inspection.ModelAssetPath,
                    dryRun: false);
                CopyMessages(pipelineReport, inspection.Report);

                if (!inspection.Report.HasErrors)
                {
                    inspection.ExistingPrefabPaths = ResolvePrefabPaths(inspection).Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray();
                    Dictionary<string, Material> materials = BuildOrUpdateMaterials(inspection, promotedPaths);
                    if (!inspection.Report.HasErrors)
                    {
                        ExoBridgePrefabUpdater.ApplyBridgeAssetsToNewPrefabs(
                            inspection,
                            inspection.ExistingPrefabPaths,
                            materials);
                    }
                }
            }

            if (!inspection.Report.HasErrors)
                ApplyAnimationOverrides(inspection, promotedPaths);

            if (!inspection.Report.HasErrors)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                inspection.Report.Info("Pacote promovido com sucesso. O original e o arquivo .blend arquivado permanecem em Incoming.", inspection.ManifestValidation.Manifest.entity.name);
            }
        }
        catch (Exception exception)
        {
            inspection.Report.Error("Falha inesperada durante a promocao: " + exception.Message, manifestPath);
        }

        return inspection.Report;
    }

    private static void ValidateProfile(ExoBridgeInspection inspection)
    {
        ExoPrefabProfile profile = inspection.Profile;
        string entityName = inspection.ManifestValidation.Manifest.entity.name;
        if (profile.entityType == ExoEntityType.Personagem)
        {
            if (profile.basePrefab == null) inspection.Report.Error("Personagem exige basePrefab no ExoPrefabProfile.", entityName);
            if (profile.characterData == null) inspection.Report.Error("Personagem exige CharacterBase no ExoPrefabProfile.", entityName);
            else
            {
                bool hasCommander = profile.characterData.commanderPrefab != null
                    && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(profile.characterData.commanderPrefab));
                bool hasTower = profile.characterData.towerPrefab != null
                    && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(profile.characterData.towerPrefab));

                // O towerPrefab conhecido de Ayame nunca e reparado pelo
                // bridge. A correcao precisa ser explicita no Inspector.
                if (string.Equals(entityName, "Ayame", StringComparison.OrdinalIgnoreCase) && !hasTower)
                    inspection.Report.Error("Ayame tem towerPrefab ausente ou orfao. Repare CharacterBase.towerPrefab manualmente antes da importacao de producao.", entityName);
                else if (hasCommander != hasTower)
                    inspection.Report.Error("CharacterBase possui apenas uma das referencias commanderPrefab/towerPrefab. Repare o estado parcial antes de importar.", entityName);
            }
        }
        else if (profile.entityType == ExoEntityType.Monstro && profile.enemyData == null)
        {
            inspection.Report.Error("Monstro exige EnemyDataSO no ExoPrefabProfile.", entityName);
        }
    }

    private static void ValidatePackageMaterialsAndAnimations(ExoBridgeInspection inspection)
    {
        ExoBridgeManifest manifest = inspection.ManifestValidation.Manifest;
        foreach (ExoBridgeMaterialEntry material in manifest.materials ?? Array.Empty<ExoBridgeMaterialEntry>())
        {
            ExoMaterialSlotBinding binding = inspection.Profile.FindMaterialBinding(material.slotName);
            if (binding == null)
                inspection.Report.Error("Slot de material \"" + material.slotName + "\" nao possui binding no ExoPrefabProfile.", manifest.entity.name);
            else if (string.IsNullOrWhiteSpace(binding.rendererPath))
                inspection.Report.Error("Binding do slot \"" + material.slotName + "\" precisa declarar rendererPath relativo ao FBX.", manifest.entity.name);
            else if (binding.rendererMaterialIndex < 0)
                inspection.Report.Error("Binding do slot \"" + material.slotName + "\" tem rendererMaterialIndex invalido.", manifest.entity.name);
        }

        if ((manifest.animations?.Length ?? 0) == 0) return;

        if (!ExoPathResolver.SupportsAssetType(inspection.Category, ExoAssetType.Animacao))
        {
            inspection.Report.Error("A categoria " + inspection.Category + " nao possui pasta canonica de Animacao; pacote com Actions e bloqueado.", manifest.entity.name);
            return;
        }

        AnimatorOverrideController overrideController = inspection.Profile.animatorController as AnimatorOverrideController;
        if (overrideController == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(overrideController)))
        {
            inspection.Report.Error("Pacote com animacoes exige AnimatorOverrideController persistido no ExoPrefabProfile. O assistente pode criar um sem criar estados.", manifest.entity.name);
            return;
        }

        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);
        foreach (ExoBridgeAnimationEntry animation in manifest.animations)
        {
            ExoAnimationBinding binding = inspection.Profile.FindAnimationBinding(animation.actionName);
            if (binding == null || binding.targetClip == null)
                inspection.Report.Error("Action \"" + animation.actionName + "\" nao possui targetClip no ExoPrefabProfile.", manifest.entity.name);
            else if (!overrides.Any(pair => pair.Key == binding.targetClip))
                inspection.Report.Error("targetClip de \"" + animation.actionName + "\" nao pertence ao AnimatorOverrideController configurado.", manifest.entity.name);
        }
    }

    private static void ValidatePackageAssetsCanBeImported(ExoBridgeInspection inspection)
    {
        ExoBridgeManifest manifest = inspection.ManifestValidation.Manifest;
        ExoBridgeFileEntry model = manifest.FindSingleFile("model");
        string modelPath = GetPackageAssetPath(inspection, model);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
            inspection.Report.Error("O FBX do pacote ainda nao foi importado pela Unity ou e invalido: \"" + modelPath + "\".", manifest.entity.name);
        else
            ValidateMaterialBindingsAgainstModel(inspection, AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));

        foreach (ExoBridgeMaterialEntry material in manifest.materials ?? Array.Empty<ExoBridgeMaterialEntry>())
        {
            string basePath = GetPackageAssetPath(inspection, manifest.FindFile(material.baseTexturePath));
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(basePath) == null)
                inspection.Report.Error("Textura base nao pode ser importada: \"" + material.baseTexturePath + "\".", manifest.entity.name);
            if (!string.IsNullOrWhiteSpace(material.shadingTexturePath))
            {
                string shadingPath = GetPackageAssetPath(inspection, manifest.FindFile(material.shadingTexturePath));
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(shadingPath) == null)
                    inspection.Report.Error("Textura de shading nao pode ser importada: \"" + material.shadingTexturePath + "\".", manifest.entity.name);
            }
        }

        foreach (ExoBridgeAnimationEntry animation in manifest.animations ?? Array.Empty<ExoBridgeAnimationEntry>())
        {
            string path = GetPackageAssetPath(inspection, manifest.FindFile(animation.filePath));
            int clipCount = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Count(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clipCount != 1)
                inspection.Report.Error("Action \"" + animation.actionName + "\" precisa conter exatamente um AnimationClip importavel; encontrados " + clipCount + ".", manifest.entity.name);
        }
    }

    private static void ValidateMaterialBindingsAgainstModel(ExoBridgeInspection inspection, GameObject importedModel)
    {
        ExoBridgeManifest manifest = inspection.ManifestValidation.Manifest;
        foreach (ExoBridgeMaterialEntry material in manifest.materials ?? Array.Empty<ExoBridgeMaterialEntry>())
        {
            ExoMaterialSlotBinding binding = inspection.Profile.FindMaterialBinding(material.slotName);
            if (binding == null || string.IsNullOrWhiteSpace(binding.rendererPath))
                continue;

            Renderer renderer = ExoBridgePrefabUpdater.FindRendererRelativeToModel(importedModel.transform, binding.rendererPath);
            if (renderer == null)
                inspection.Report.Error("Binding \"" + material.slotName + "\" aponta para rendererPath inexistente no FBX: \"" + binding.rendererPath + "\".", manifest.entity.name);
            else if (binding.rendererMaterialIndex >= renderer.sharedMaterials.Length)
                inspection.Report.Error("Binding \"" + material.slotName + "\" usa indice " + binding.rendererMaterialIndex + " que nao existe no Renderer exportado \"" + binding.rendererPath + "\".", manifest.entity.name);
        }
    }

    private static void ValidateExistingState(ExoBridgeInspection inspection)
    {
        List<string> paths = inspection.ExistingPrefabPaths.ToList();
        if (paths.Count == 0) return;

        int existing = paths.Count(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null);
        if (existing > 0 && existing != paths.Count)
        {
            inspection.Report.Error("Estado parcial de prefabs detectado. Repare a entidade antes de reexportar; a ponte nao reconstrui metade de uma entidade.", inspection.ManifestValidation.Manifest.entity.name);
            return;
        }

        if (existing == paths.Count)
            ExoBridgePrefabUpdater.ValidateExistingPrefabs(inspection, paths);
    }

    private static Dictionary<string, string> PromotePackage(ExoBridgeInspection inspection)
    {
        ExoBridgeManifest manifest = inspection.ManifestValidation.Manifest;
        Dictionary<string, string> promoted = new Dictionary<string, string>(StringComparer.Ordinal);
        string modelFolder = inspection.Config.ResolveFolder(inspection.Category, manifest.entity.name, ExoAssetType.Modelos, inspection.Report);
        string textureFolder = inspection.Config.ResolveFolder(inspection.Category, manifest.entity.name, ExoAssetType.Texturas, inspection.Report);
        string animationFolder = ExoPathResolver.SupportsAssetType(inspection.Category, ExoAssetType.Animacao)
            ? inspection.Config.ResolveFolder(inspection.Category, manifest.entity.name, ExoAssetType.Animacao, inspection.Report)
            : null;

        foreach (ExoBridgeFileEntry file in manifest.files)
        {
            if (string.Equals(file.kind, "source_blend_archive", StringComparison.Ordinal))
                continue;

            string destinationFolder = string.Equals(file.kind, "model", StringComparison.Ordinal) ? modelFolder
                : string.Equals(file.kind, "texture", StringComparison.Ordinal) ? textureFolder
                : string.Equals(file.kind, "animation", StringComparison.Ordinal) ? animationFolder
                : null;

            if (string.IsNullOrEmpty(destinationFolder))
            {
                inspection.Report.Error("A categoria nao aceita o arquivo \"" + file.relativePath + "\" do tipo \"" + file.kind + "\".", manifest.entity.name);
                continue;
            }

            string destinationPath = ExoPathResolver.Normalize(Path.Combine(destinationFolder, Path.GetFileName(file.relativePath)));
            CopyWithBackup(inspection, GetPackageAssetPath(inspection, file), destinationPath);
            promoted[file.relativePath] = destinationPath;
        }

        ExoBridgeFileEntry model = manifest.FindSingleFile("model");
        inspection.ModelAssetPath = model == null ? null : promoted.TryGetValue(model.relativePath, out string path) ? path : null;
        if (string.IsNullOrEmpty(inspection.ModelAssetPath))
            inspection.Report.Error("O FBX principal nao foi promovido.", manifest.entity.name);

        return promoted;
    }

    private static void CopyWithBackup(ExoBridgeInspection inspection, string sourcePath, string destinationPath)
    {
        string sourceFullPath = ExoBridgeManifestReader.ToFullPath(sourcePath);
        string destinationFullPath = ExoBridgeManifestReader.ToFullPath(destinationPath);
        string destinationDirectory = Path.GetDirectoryName(destinationFullPath);
        Directory.CreateDirectory(destinationDirectory);

        if (File.Exists(destinationFullPath))
        {
            string backupPath = ExoPathResolver.Normalize(Path.Combine(
                BackupRoot,
                inspection.ManifestValidation.Manifest.packageId,
                destinationPath.Substring("Assets/".Length)));
            string backupFullPath = ExoBridgeManifestReader.ToFullPath(backupPath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupFullPath));
            File.Copy(destinationFullPath, backupFullPath, overwrite: true);
            if (File.Exists(destinationFullPath + ".meta"))
                File.Copy(destinationFullPath + ".meta", backupFullPath + ".meta", overwrite: true);
        }

        File.Copy(sourceFullPath, destinationFullPath, overwrite: true);
        inspection.Report.Info("Promovido por copia \"" + sourcePath + "\" para \"" + destinationPath + "\"; pacote original preservado.", inspection.ManifestValidation.Manifest.entity.name);
    }

    private static Dictionary<string, Material> BuildOrUpdateMaterials(ExoBridgeInspection inspection, IReadOnlyDictionary<string, string> promotedPaths)
    {
        Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        ExoBridgeManifest manifest = inspection.ManifestValidation.Manifest;
        string materialFolder = inspection.Config.ResolveFolder(inspection.Category, manifest.entity.name, ExoAssetType.Materiais, inspection.Report);
        Directory.CreateDirectory(ExoBridgeManifestReader.ToFullPath(materialFolder));

        Shader shader = Shader.Find(ExoPrefabBuilder.ToonShaderName);
        if (shader == null)
        {
            inspection.Report.Error("Shader \"" + ExoPrefabBuilder.ToonShaderName + "\" nao encontrado; nenhum material foi alterado.", manifest.entity.name);
            return materials;
        }

        foreach (ExoBridgeMaterialEntry entry in manifest.materials ?? Array.Empty<ExoBridgeMaterialEntry>())
        {
            ExoMaterialSlotBinding binding = inspection.Profile.FindMaterialBinding(entry.slotName);
            if (binding == null || !promotedPaths.TryGetValue(entry.baseTexturePath, out string baseTexturePath))
                continue;

            string outputName = string.IsNullOrWhiteSpace(binding.outputMaterialName) ? entry.slotName : binding.outputMaterialName;
            string materialPath = ExoPathResolver.Normalize(Path.Combine(materialFolder, SanitizeFileName(manifest.entity.name + "_" + outputName) + "_Mat.mat"));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexturePath));
            if (!string.IsNullOrWhiteSpace(entry.shadingTexturePath) && promotedPaths.TryGetValue(entry.shadingTexturePath, out string shadingTexturePath))
                material.SetTexture("_shadingMap", AssetDatabase.LoadAssetAtPath<Texture2D>(shadingTexturePath));
            material.SetColor("_ShadowColor", inspection.Profile.shadowColor);
            material.SetColor("_OuterShadowColor", inspection.Profile.outerShadowColor);
            material.SetFloat("_OuterShadowWidth", inspection.Profile.outerShadowWidth);
            material.SetFloat("_LightSmooth", inspection.Profile.lightSmooth);
            EditorUtility.SetDirty(material);
            materials[entry.slotName] = material;
        }

        return materials;
    }

    private static void ApplyAnimationOverrides(ExoBridgeInspection inspection, IReadOnlyDictionary<string, string> promotedPaths)
    {
        ExoBridgeManifest manifest = inspection.ManifestValidation.Manifest;
        if ((manifest.animations?.Length ?? 0) == 0) return;

        AnimatorOverrideController controller = inspection.Profile.animatorController as AnimatorOverrideController;
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
        controller.GetOverrides(overrides);

        foreach (ExoBridgeAnimationEntry action in manifest.animations)
        {
            if (!promotedPaths.TryGetValue(action.filePath, out string animationAssetPath))
            {
                inspection.Report.Error("Action promovida nao encontrada: \"" + action.actionName + "\".", manifest.entity.name);
                continue;
            }

            List<AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(animationAssetPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToList();
            if (clips.Count != 1)
            {
                inspection.Report.Error("Action \"" + action.actionName + "\" precisa importar exatamente um AnimationClip; encontrados " + clips.Count + ".", manifest.entity.name);
                continue;
            }

            ExoAnimationBinding binding = inspection.Profile.FindAnimationBinding(action.actionName);
            int index = overrides.FindIndex(pair => pair.Key == binding.targetClip);
            if (index < 0)
            {
                inspection.Report.Error("O targetClip de \"" + action.actionName + "\" nao pertence ao AnimatorOverrideController configurado.", manifest.entity.name);
                continue;
            }
            overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[index].Key, clips[0]);
        }

        if (inspection.Report.HasErrors) return;
        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
        ExoBridgePrefabUpdater.AssignOverrideController(inspection, inspection.ExistingPrefabPaths, controller);
    }

    private static IReadOnlyList<string> ResolvePrefabPaths(ExoBridgeInspection inspection)
    {
        if (inspection.Profile == null) return Array.Empty<string>();
        if (inspection.Profile.entityType == ExoEntityType.Personagem)
        {
            if (inspection.Profile.characterData == null) return Array.Empty<string>();
            return new[]
            {
                AssetDatabase.GetAssetPath(inspection.Profile.characterData.commanderPrefab),
                AssetDatabase.GetAssetPath(inspection.Profile.characterData.towerPrefab)
            };
        }
        if (inspection.Profile.entityType == ExoEntityType.Monstro)
        {
            return inspection.Profile.enemyData == null
                ? Array.Empty<string>()
                : new[] { AssetDatabase.GetAssetPath(inspection.Profile.enemyData.enemyPrefab) };
        }

        ExoBridgeFileEntry model = inspection.ManifestValidation.Manifest.FindSingleFile("model");
        if (model == null || inspection.Config == null) return Array.Empty<string>();
        string prefabsFolder = inspection.Config.ResolveFolder(inspection.Category, inspection.ManifestValidation.Manifest.entity.name, ExoAssetType.Prefabs);
        string prefabName = ExoNaming.GenericPrefabFileName(Path.GetFileNameWithoutExtension(model.relativePath));
        return new[] { ExoPathResolver.Normalize(Path.Combine(prefabsFolder, prefabName)) };
    }

    private static string GetPackageAssetPath(ExoBridgeInspection inspection, ExoBridgeFileEntry file)
    {
        return file == null ? null : ExoPathResolver.Normalize(Path.Combine(inspection.ManifestValidation.PackageRoot, file.relativePath));
    }

    private static string GetExpectedPromotedModelPath(ExoBridgeInspection inspection)
    {
        ExoBridgeFileEntry model = inspection.ManifestValidation.Manifest.FindSingleFile("model");
        if (model == null || inspection.Config == null) return null;

        string folder = inspection.Config.ResolveFolder(
            inspection.Category,
            inspection.ManifestValidation.Manifest.entity.name,
            ExoAssetType.Modelos,
            inspection.Report);
        return ExoPathResolver.Normalize(Path.Combine(folder, Path.GetFileName(model.relativePath)));
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }

    private static void CopyMessages(ExoBuildReport source, ExoBuildReport target)
    {
        if (source == null) return;
        foreach (ExoBuildMessage message in source.Messages)
            target.Add(message.Severity, message.Message, message.Context);
    }
}

/// <summary>
/// Atualizacao in-place exclusiva para pacotes ja aprovados. Antes de destruir
/// o modelo antigo, recusa qualquer referencia serializada de componentes que
/// fiquem fora da subarvore do modelo e apontem para dentro dela.
/// </summary>
internal static class ExoBridgePrefabUpdater
{
    internal static void ValidateExistingPrefabs(ExoBridgeInspection inspection, IEnumerable<string> prefabPaths)
    {
        foreach (string prefabPath in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                inspection.Report.Error("Nao foi possivel abrir prefab para preflight: \"" + prefabPath + "\".", inspection.ManifestValidation.Manifest.entity.name);
                continue;
            }
            try
            {
                Transform modelRoot = FindModelRoot(root, inspection.Profile.entityType);
                if (modelRoot == null)
                {
                    inspection.Report.Error("Prefab \"" + prefabPath + "\" nao possui uma subarvore de modelo identificavel; atualizacao bloqueada.", inspection.ManifestValidation.Manifest.entity.name);
                    continue;
                }
                ValidateExternalModelReferences(inspection, root, modelRoot, prefabPath);
                ValidateModelSubtreeComponents(inspection, modelRoot, prefabPath);
                ValidateMaterialIndexes(inspection, modelRoot, prefabPath);
                if ((inspection.ManifestValidation.Manifest.animations?.Length ?? 0) > 0
                    && modelRoot.GetComponentInChildren<Animator>(true) == null)
                {
                    inspection.Report.Error("Prefab \"" + prefabPath + "\" nao possui Animator dentro do modelo para receber as Actions declaradas.", inspection.ManifestValidation.Manifest.entity.name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    internal static void UpdateExistingPrefabs(ExoBridgeInspection inspection, IEnumerable<string> prefabPaths, string modelAssetPath, IReadOnlyDictionary<string, Material> materials)
    {
        GameObject importedModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
        if (importedModel == null)
        {
            inspection.Report.Error("FBX promovido nao pode ser carregado: \"" + modelAssetPath + "\".", inspection.ManifestValidation.Manifest.entity.name);
            return;
        }

        foreach (string prefabPath in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                inspection.Report.Error("Nao foi possivel abrir prefab para atualizar: \"" + prefabPath + "\".", inspection.ManifestValidation.Manifest.entity.name);
                continue;
            }
            try
            {
                Transform oldModel = FindModelRoot(root, inspection.Profile.entityType);
                if (oldModel == null)
                {
                    inspection.Report.Error("Modelo antigo nao encontrado em \"" + prefabPath + "\"; prefab permaneceu intacto.", inspection.ManifestValidation.Manifest.entity.name);
                    continue;
                }

                ReplaceModel(oldModel, importedModel);
                ApplyMaterials(root, inspection.Profile, materials, inspection.Report, inspection.ManifestValidation.Manifest.entity.name);
                if (!inspection.Report.HasErrors)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    inspection.Report.Info("Prefab atualizado in-place: \"" + prefabPath + "\".", inspection.ManifestValidation.Manifest.entity.name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    internal static void ApplyBridgeAssetsToNewPrefabs(ExoBridgeInspection inspection, IEnumerable<string> prefabPaths, IReadOnlyDictionary<string, Material> materials)
    {
        foreach (string prefabPath in prefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) continue;
            try
            {
                ApplyMaterials(root, inspection.Profile, materials, inspection.Report, inspection.ManifestValidation.Manifest.entity.name);
                if (!inspection.Report.HasErrors) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    internal static void AssignOverrideController(ExoBridgeInspection inspection, IEnumerable<string> prefabPaths, AnimatorOverrideController controller)
    {
        foreach (string prefabPath in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) continue;
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    inspection.Report.Error("Prefab \"" + prefabPath + "\" nao possui Animator para receber o AnimatorOverrideController.", inspection.ManifestValidation.Manifest.entity.name);
                    continue;
                }
                animator.runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static Transform FindModelRoot(GameObject root, ExoEntityType entityType)
    {
        if (entityType == ExoEntityType.Personagem)
        {
            Transform pivot = root.transform.Find("Pivot");
            return pivot != null && pivot.childCount == 1 ? pivot.GetChild(0) : null;
        }

        foreach (Transform child in root.transform)
        {
            if (PrefabUtility.GetPrefabAssetType(child.gameObject) == PrefabAssetType.Model)
                return child;
        }
        return null;
    }

    private static void ValidateExternalModelReferences(ExoBridgeInspection inspection, GameObject root, Transform modelRoot, string prefabPath)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.transform.IsChildOf(modelRoot)) continue;
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (!PointsInsideModel(property.objectReferenceValue, modelRoot)) continue;

                inspection.Report.Error(
                    "Atualizacao bloqueada: \"" + component.GetType().Name + "." + property.propertyPath + "\" em \"" + prefabPath + "\" aponta para dentro do modelo antigo.",
                    inspection.ManifestValidation.Manifest.entity.name);
            }
        }
    }

    private static bool PointsInsideModel(UnityEngine.Object value, Transform modelRoot)
    {
        if (value is GameObject gameObject) return gameObject.transform.IsChildOf(modelRoot);
        if (value is Component component) return component.transform.IsChildOf(modelRoot);
        return false;
    }

    private static void ValidateModelSubtreeComponents(ExoBridgeInspection inspection, Transform modelRoot, string prefabPath)
    {
        foreach (Component component in modelRoot.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || component is Renderer || component is MeshFilter
                || component is Animator || component is NetworkAnimator)
            {
                continue;
            }

            inspection.Report.Error(
                "Atualizacao bloqueada: componente Unity-only \"" + component.GetType().Name + "\" dentro do modelo de \"" + prefabPath + "\" nao possui estrategia de migracao declarada.",
                inspection.ManifestValidation.Manifest.entity.name);
        }
    }

    private static void ValidateMaterialIndexes(ExoBridgeInspection inspection, Transform modelRoot, string prefabPath)
    {
        foreach (ExoMaterialSlotBinding binding in inspection.Profile.materialSlotBindings ?? new List<ExoMaterialSlotBinding>())
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.rendererPath)) continue;
            Renderer renderer = FindRendererRelativeToModel(modelRoot, binding.rendererPath);
            if (renderer == null)
                inspection.Report.Error("Binding \"" + binding.sourceSlot + "\" aponta para rendererPath inexistente \"" + binding.rendererPath + "\" em \"" + prefabPath + "\".", inspection.ManifestValidation.Manifest.entity.name);
            else if (binding.rendererMaterialIndex >= renderer.sharedMaterials.Length)
                inspection.Report.Error("Binding \"" + binding.sourceSlot + "\" usa indice " + binding.rendererMaterialIndex + " que nao existe em Renderer \"" + binding.rendererPath + "\" de \"" + prefabPath + "\".", inspection.ManifestValidation.Manifest.entity.name);
        }
    }

    internal static Renderer FindRendererRelativeToModel(Transform modelRoot, string rendererPath)
    {
        if (modelRoot == null || string.IsNullOrWhiteSpace(rendererPath)) return null;
        Transform target = rendererPath == "." ? modelRoot : modelRoot.Find(rendererPath);
        return target == null ? null : target.GetComponent<Renderer>();
    }

    private static void ReplaceModel(Transform oldModel, GameObject importedModel)
    {
        Animator oldAnimator = oldModel.GetComponentInChildren<Animator>(true);
        NetworkAnimator oldNetworkAnimator = oldModel.GetComponentInChildren<NetworkAnimator>(true);
        AnimatorSettingsSnapshot animatorSettings = AnimatorSettingsSnapshot.Capture(oldAnimator);
        bool hadNetworkAnimator = oldNetworkAnimator != null;
        Transform parent = oldModel.parent;
        int siblingIndex = oldModel.GetSiblingIndex();
        UnityEngine.Object instance = PrefabUtility.InstantiatePrefab(importedModel);
        GameObject newModel = instance as GameObject;
        if (newModel == null) throw new InvalidOperationException("Nao foi possivel instanciar o FBX promovido.");
        newModel.transform.SetParent(parent, false);
        newModel.transform.SetSiblingIndex(siblingIndex);
        UnityEngine.Object.DestroyImmediate(oldModel.gameObject);
        PreserveAnimationComponents(newModel, animatorSettings, hadNetworkAnimator);
    }

    private static void PreserveAnimationComponents(GameObject newModel, AnimatorSettingsSnapshot oldAnimator, bool hadNetworkAnimator)
    {
        if (oldAnimator == null && !hadNetworkAnimator) return;

        Animator animator = newModel.GetComponent<Animator>();
        if (animator == null) animator = newModel.AddComponent<Animator>();
        if (oldAnimator != null)
        {
            // O Avatar vem do novo FBX. Mantemos apenas configuracoes Unity
            // que pertencem ao prefab, em vez de referenciar o Avatar antigo.
            animator.runtimeAnimatorController = oldAnimator.Controller;
            animator.applyRootMotion = oldAnimator.ApplyRootMotion;
            animator.updateMode = oldAnimator.UpdateMode;
            animator.cullingMode = oldAnimator.CullingMode;
            animator.fireEvents = oldAnimator.FireEvents;
            animator.keepAnimatorStateOnDisable = oldAnimator.KeepControllerStateOnDisable;
            animator.logWarnings = oldAnimator.LogWarnings;
        }

        if (hadNetworkAnimator)
        {
            NetworkAnimator networkAnimator = newModel.GetComponent<NetworkAnimator>();
            if (networkAnimator == null) networkAnimator = newModel.AddComponent<NetworkAnimator>();
            networkAnimator.Animator = animator;
        }
    }

    private sealed class AnimatorSettingsSnapshot
    {
        internal RuntimeAnimatorController Controller;
        internal bool ApplyRootMotion;
        internal AnimatorUpdateMode UpdateMode;
        internal AnimatorCullingMode CullingMode;
        internal bool FireEvents;
        internal bool KeepControllerStateOnDisable;
        internal bool LogWarnings;

        internal static AnimatorSettingsSnapshot Capture(Animator animator)
        {
            if (animator == null) return null;
            return new AnimatorSettingsSnapshot
            {
                Controller = animator.runtimeAnimatorController,
                ApplyRootMotion = animator.applyRootMotion,
                UpdateMode = animator.updateMode,
                CullingMode = animator.cullingMode,
                FireEvents = animator.fireEvents,
                KeepControllerStateOnDisable = animator.keepAnimatorStateOnDisable,
                LogWarnings = animator.logWarnings,
            };
        }
    }

    private static void ApplyMaterials(GameObject root, ExoPrefabProfile profile, IReadOnlyDictionary<string, Material> materials, ExoBuildReport report, string entityName)
    {
        Transform modelRoot = FindModelRoot(root, profile.entityType);
        if (modelRoot == null)
        {
            report.Error("Prefab nao possui uma raiz de modelo identificavel para aplicar os materiais explicitamente mapeados.", entityName);
            return;
        }

        foreach (ExoMaterialSlotBinding binding in profile.materialSlotBindings ?? new List<ExoMaterialSlotBinding>())
        {
            if (binding == null || !materials.TryGetValue(binding.sourceSlot, out Material material)) continue;
            Renderer renderer = FindRendererRelativeToModel(modelRoot, binding.rendererPath);
            if (renderer == null || binding.rendererMaterialIndex < 0 || binding.rendererMaterialIndex >= renderer.sharedMaterials.Length)
            {
                report.Error("Renderer \"" + binding.rendererPath + "\" nao possui o indice configurado para o slot \"" + binding.sourceSlot + "\".", entityName);
                continue;
            }

            Material[] shared = renderer.sharedMaterials;
            shared[binding.rendererMaterialIndex] = material;
            renderer.sharedMaterials = shared;
        }
    }
}
