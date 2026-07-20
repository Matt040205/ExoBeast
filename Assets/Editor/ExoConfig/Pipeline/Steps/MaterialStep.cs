using System.IO;
using UnityEditor;
using UnityEngine;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Cria ou atualiza o material da entidade usando ExoPrefabBuilder.ToonShaderName
/// ("Shader Graphs/ToonExobeasts") para TODAS as categorias - decisao da Fase
/// 4, ver comentario em ExoPrefabBuilder.ToonShaderName para a justificativa
/// completa (o antigo "Toon/Toon" para Environment na verdade resolvia,
/// silenciosamente, para um shader generico de um pacote de terceiros -
/// confirmado em runtime nesta fase - nao para o fallback de URP/Lit que se
/// presumia).
///
/// Sem fallback silencioso: se o shader nao for encontrado, este step
/// reporta Error e para (nao chama ExoPrefabBuilder.BuildMaterial) - o
/// pipeline inteiro para em seguida, antes de BuildPrefabStep (ver
/// ExoBuildPipeline.Run), entao nenhum prefab e montado com material
/// ausente/errado.
///
/// A validacao do shader roda mesmo em DryRun (Shader.Find e uma consulta,
/// nao uma escrita em disco) - "sem fallback silencioso" nao e uma garantia
/// que so vale na execucao real. Ja a criacao/atualizacao de fato do
/// material delega para ExoPrefabBuilder.BuildMaterial (agora internal),
/// SOMENTE fora de DryRun.
/// </summary>
public sealed class MaterialStep : IExoBuildStep
{
    public string Name => "Material";

    public void Execute(ExoBuildContext context)
    {
        Shader shader = Shader.Find(ExoPrefabBuilder.ToonShaderName);
        if (shader == null)
        {
            context.Report.Error(
                "Shader \"" + ExoPrefabBuilder.ToonShaderName + "\" nao encontrado no projeto. Abortando - Fase 4 removeu o fallback silencioso para outro shader.",
                context.Nome);
            return;
        }

        string matPath = ExoPathResolver.Normalize(Path.Combine(context.MateriaisFolder, ExoNaming.MaterialFileName(context.FbxFileName)));

        if (context.DryRun)
        {
            bool wouldUpdate = AssetDatabase.LoadAssetAtPath<Material>(matPath) != null;
            context.Report.Info(
                "[DryRun] Material seria " + (wouldUpdate ? "atualizado" : "criado") + " em \"" + matPath + "\" com shader \"" + ExoPrefabBuilder.ToonShaderName + "\".",
                context.Nome);
            return;
        }

        context.Material = ExoPrefabBuilder.BuildMaterial(
            context.DestFbxPath, context.MateriaisFolder, context.FbxFileName, context.Profile, context.EntityType, context.Report);

        if (context.Material == null && !context.Report.HasErrors)
        {
            // Guarda defensiva: hoje BuildMaterial so devolve null quando ja
            // reportou Error (shader ausente), mas nao deveria deixar o
            // pipeline seguir para BuildPrefabStep em silencio caso isso
            // mude no futuro sem reportar nada.
            context.Report.Error("Falha desconhecida ao criar/atualizar o material.", context.Nome);
        }
    }
}
