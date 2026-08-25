using System.IO;
using UnityEditor;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Move o FBX selecionado e, se existir, a textura irma "[Nome]T.png" para as
/// pastas resolvidas por ResolvePathsStep.
///
/// Cria as pastas de destino se ainda nao existirem (Directory.CreateDirectory,
/// mesmo padrao ja usado por ExoPrefabBuilder.BuildCharacterPrefab para
/// matFolder/prefabFolder e por ExoToolConfig.LoadOrCreate). O codigo
/// original (ExoPrefabMenu.ExecutarOrganizar antes da Fase 4) NAO fazia isso:
/// AssetDatabase.MoveAsset falha (retorna uma string de erro, nao lanca
/// excecao) quando a pasta de destino nao existe, e esse retorno era
/// ignorado - o FBX simplesmente nao era movido, em silencio, na primeira vez
/// que uma entidade nova era organizada para uma pasta que ainda nao existia.
///
/// Higiene da Fase 4 aplicada aqui: o retorno de AssetDatabase.MoveAsset
/// agora e sempre checado (ver MoveAsset abaixo) - erro vira
/// context.Report.Error e aborta o step (nenhum step seguinte roda, ver
/// ExoBuildPipeline.Run).
/// </summary>
public sealed class ImportAssetsStep : IExoBuildStep
{
    public string Name => "ImportAssets";

    public void Execute(ExoBuildContext context)
    {
        string destModelPath = ExoPathResolver.Normalize(Path.Combine(context.ModelosFolder, ExoNaming.ModelFileName(context.FbxFileName)));

        if (context.AssetsAlreadyPromoted)
        {
            if (!string.Equals(context.SourceFbxPath, destModelPath, System.StringComparison.OrdinalIgnoreCase)
                || (!context.DryRun && !File.Exists(destModelPath)))
            {
                context.Report.Error(
                    "Ponte Exo Bridge esperava o FBX promovido em \"" + destModelPath + "\", mas ele nao foi encontrado.",
                    context.Nome);
                return;
            }

            context.DestFbxPath = destModelPath;
            if (context.DryRun)
                context.Report.Info("[DryRun] Exo Bridge promoveria o FBX para \"" + destModelPath + "\" sem mover o pacote de evidencia.", context.Nome);
            string promotedTexturePath = ExoPathResolver.Normalize(Path.Combine(context.TexturasFolder, ExoNaming.TextureFileName(context.FbxFileName)));
            if (File.Exists(promotedTexturePath))
                context.DestTexturePath = promotedTexturePath;

            context.Report.Info("FBX e texturas ja foram promovidos pelo Exo Bridge; ImportAssets nao movera o pacote de evidencia.", context.Nome);
            return;
        }

        if (!MoveAsset(context, context.SourceFbxPath, destModelPath, context.ModelosFolder, "FBX"))
            return;
        context.DestFbxPath = destModelPath;

        string textureFileName = ExoNaming.TextureFileName(context.FbxFileName);
        string sourceTexturePath = ExoPathResolver.Normalize(Path.Combine(context.SourceFolderPath, textureFileName));

        if (!File.Exists(sourceTexturePath))
        {
            context.Report.Info("Nenhuma textura encontrada em \"" + sourceTexturePath + "\" - pulando.", context.Nome);
        }
        else
        {
            string destTexturePath = ExoPathResolver.Normalize(Path.Combine(context.TexturasFolder, textureFileName));
            if (!MoveAsset(context, sourceTexturePath, destTexturePath, context.TexturasFolder, "textura"))
                return;
            context.DestTexturePath = destTexturePath;
        }

        if (!context.DryRun)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Move um unico asset, criando a pasta de destino se necessario. Devolve
    /// false (e ja registra o Error no report) se a pasta nao pode ser
    /// preparada ou se AssetDatabase.MoveAsset devolver uma string de erro
    /// nao vazia - o chamador deve parar de processar este FBX nesse caso.
    /// Em DryRun, so relata o que aconteceria (inclusive se a pasta de
    /// destino precisaria ser criada) e sempre devolve true - dry run nunca
    /// "falha" por conta propria, so descreve a acao que a execucao real
    /// faria.
    /// </summary>
    private static bool MoveAsset(ExoBuildContext context, string sourcePath, string destPath, string destFolder, string descricao)
    {
        bool folderMissing = !Directory.Exists(destFolder);

        if (context.DryRun)
        {
            string folderNote = folderMissing ? " (criaria a pasta \"" + destFolder + "\")" : "";
            context.Report.Info("[DryRun] Moveria " + descricao + " de \"" + sourcePath + "\" para \"" + destPath + "\"" + folderNote + ".", context.Nome);
            return true;
        }

        if (folderMissing)
        {
            Directory.CreateDirectory(destFolder);
            AssetDatabase.Refresh();
        }

        string error = AssetDatabase.MoveAsset(sourcePath, destPath);
        if (!string.IsNullOrEmpty(error))
        {
            context.Report.Error("Falha ao mover " + descricao + " de \"" + sourcePath + "\" para \"" + destPath + "\": " + error, context.Nome);
            return false;
        }

        context.Report.Info(char.ToUpperInvariant(descricao[0]) + descricao.Substring(1) + " movido(a) para \"" + destPath + "\".", context.Nome);
        return true;
    }
}
