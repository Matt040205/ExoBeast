using System.IO;
using UnityEditor;
using UnityEngine;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Fase 7 da refatoracao Exo Config. Roda DEPOIS de BuildPrefabStep (nao
/// antes): o componente Animator so passa a existir na hierarquia depois que
/// ExoPrefabBuilder monta e SALVA o(s) prefab(s) (ver
/// ExoPrefabBuilder.SetupMeshChildComponents, chamado por
/// ReplaceModelUnderPivot/Personagem e ConfigureAsTower/Torre; e
/// ExoPrefabBuilder.ConfigureAsEnemy/Monstro, que adiciona o Animator direto
/// no root) - antes de BuildPrefabStep rodar, o pipeline nao tem nenhum
/// GameObject vivo em memoria (so caminhos de pasta), entao nao ha Animator
/// nenhum para este step examinar. Confirmado lendo BuildCharacterPrefab/
/// BuildOrUpdateCharacterVariant/ConfigureAsTower/ConfigureAsEnemy nesta
/// fase, nao presumido.
///
/// Duas responsabilidades, nesta ordem:
///
/// 1. MOVER animacoes soltas: qualquer arquivo ".anim" encontrado direto em
///    context.SourceFolderPath (a pasta do FBX selecionado - mesmo
///    "diretorio de origem" que ImportAssetsStep usa para achar a textura
///    irma) vai para context.AnimacaoFolder, preservando o nome do arquivo
///    (ao contrario do modelo/textura/material, clipes de animacao nao tem
///    UM nome previsivel derivado do fbxName - um personagem tem VARIOS
///    clipes com nomes autorais, ex.: "AranhaAttackEDIT.anim",
///    "AranhaWalkEdit.anim", confirmados em Assets/Entidades/Inimigos/Aranha/
///    nesta fase - entao so faz sentido RELOCAR, nunca RENOMEAR).
///
///    Escopo DELIBERADAMENTE limitado a ".anim" - NAO tenta mover FBX extras
///    encontrados na mesma pasta como "FBX de animacao". Motivo: um ".anim"
///    so pode ser uma AnimationClip (extensao inambigua), mas um ".fbx"
///    solto ao lado do modelo principal pode ser QUALQUER coisa (outro
///    modelo, uma peca separada, um acessorio) - confirmado nesta fase que
///    Assets/Entidades/Inimigos/Aranha/ tem "Aranhaaa.fbx" ao lado de
///    "Aranha.fbx" sem NENHUM sinal de nome que diga "este e o de animacao".
///    Adivinhar errado moveria/reclassificaria em silencio um asset nao
///    relacionado - pior do que deixar um FBX solto para o designer mover a
///    mao (que so um problema cosmetico de organizacao, nao um dado
///    corrompido). Documentado aqui como decisao deliberada, nao lacuna.
///
/// 2. RESOLVER/ATRIBUIR o RuntimeAnimatorController de cada prefab montado
///    nesta execucao (context.BuiltPrefabPaths - Personagem+Torre ou so
///    Monstro), por convencao (ExoNaming.AnimatorControllerFileName,
///    "&lt;Nome&gt;Animator.controller" dentro de context.AnimacaoFolder).
///    ExoPrefabProfile.animatorController, quando preenchido, ja foi
///    aplicado ANTES deste step rodar (dentro de SetupMeshChildComponents/
///    ConfigureAsEnemy, chamados por BuildPrefabStep) - este step nunca
///    sobrescreve um Animator que ja tem runtimeAnimatorController != null,
///    entao o override do profile vence a convencao automaticamente, sem
///    precisar duplicar aquela logica de precedencia aqui (so observa o
///    RESULTADO: "ja tem controller? entao nao mexe").
///
///    Escopo explicito: so ORGANIZA e ATRIBUI uma referencia a um Animator
///    Controller EXISTENTE. Nunca cria Animator Controller nem maquina de
///    estados nova - controllers sao sempre autorais (confirmado: o unico
///    controller real do projeto, Assets/Personagens/Ayame/Animação/AyameAnimator.controller,
///    foi colocado a mao). Entidades sem controller (Brunhilde/Coral,
///    confirmadas sem nenhum arquivo em Assets/Personagens/&lt;Nome&gt;/Animação/
///    nesta fase) degradam com Report.Warning, nunca Error - a entidade
///    continua montada normalmente, so sem controller atribuido.
/// </summary>
public sealed class AnimatorStep : IExoBuildStep
{
    public string Name => "Animator";

    public void Execute(ExoBuildContext context)
    {
        if (context.AnimacaoFolder == null)
        {
            context.Report.Info(
                "Categoria \"" + context.Categoria + "\" nao usa pasta de Animacao (ExoPathResolver.SupportsAssetType) - nada a organizar/atribuir.",
                context.Nome);
            return;
        }

        if (!context.AssetsAlreadyPromoted)
            MoveLooseAnimationFiles(context);
        else
            context.Report.Info("Animacoes ja foram promovidas pelo Exo Bridge; AnimatorStep nao movera arquivos do pacote.", context.Nome);

        string controllerFileName = ExoNaming.AnimatorControllerFileName(context.Nome);
        string controllerPath = ExoPathResolver.Normalize(Path.Combine(context.AnimacaoFolder, controllerFileName));

        if (context.DryRun)
        {
            context.Report.Info(
                "[DryRun] Animator Controller de \"" + context.Nome + "\" seria resolvido em \"" + controllerPath + "\" (convencao) " +
                "e atribuido aos prefabs montados, exceto onde ExoPrefabProfile.animatorController ja define um override explicito.",
                context.Nome);
            return;
        }

        AssignAnimatorControllers(context, controllerPath, controllerFileName);
    }

    /// <summary>
    /// Ve comentario da classe, item 1. Nao recursivo (SearchOption.TopDirectoryOnly)
    /// - mesmo estilo raso de ImportAssetsStep, que so olha a pasta imediata
    /// do FBX selecionado, nunca subpastas.
    /// </summary>
    private static void MoveLooseAnimationFiles(ExoBuildContext context)
    {
        if (!Directory.Exists(context.SourceFolderPath))
            return;

        string[] animFiles = Directory.GetFiles(context.SourceFolderPath, "*.anim", SearchOption.TopDirectoryOnly);
        if (animFiles.Length == 0)
            return;

        if (!context.DryRun && !Directory.Exists(context.AnimacaoFolder))
        {
            Directory.CreateDirectory(context.AnimacaoFolder);
            AssetDatabase.Refresh();
        }

        foreach (string animFile in animFiles)
        {
            string sourcePath = ExoPathResolver.Normalize(animFile);
            string destPath = ExoPathResolver.Normalize(Path.Combine(context.AnimacaoFolder, Path.GetFileName(animFile)));

            if (context.DryRun)
            {
                context.Report.Info("[DryRun] Moveria animacao de \"" + sourcePath + "\" para \"" + destPath + "\".", context.Nome);
                continue;
            }

            string error = AssetDatabase.MoveAsset(sourcePath, destPath);
            if (!string.IsNullOrEmpty(error))
            {
                context.Report.Warning("Falha ao mover animacao de \"" + sourcePath + "\" para \"" + destPath + "\": " + error, context.Nome);
                continue;
            }

            context.Report.Info("Animacao movida para \"" + destPath + "\".", context.Nome);
        }

        if (!context.DryRun)
            AssetDatabase.SaveAssets();
    }

    /// <summary>Ve comentario da classe, item 2.</summary>
    private static void AssignAnimatorControllers(ExoBuildContext context, string controllerPath, string controllerFileName)
    {
        if (context.BuiltPrefabPaths.Count == 0)
        {
            context.Report.Info("Nenhum prefab foi montado nesta execucao - nada para atribuir Animator Controller.", context.Nome);
            return;
        }

        RuntimeAnimatorController conventionController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (conventionController == null)
        {
            context.Report.Warning(
                "Nenhum Animator Controller encontrado em \"" + controllerPath + "\" (convencao \"" + controllerFileName + "\"). " +
                "Controllers sao sempre autorais - se \"" + context.Nome + "\" deveria ter animacao, crie/posicione o controller manualmente " +
                "nesse caminho e rode Organizar novamente. Prosseguindo sem atribuir controller (nao bloqueia a montagem do prefab).",
                context.Nome);
        }

        foreach (string prefabPath in context.BuiltPrefabPaths)
            AssignAnimatorController(context, prefabPath, conventionController);
    }

    private static void AssignAnimatorController(ExoBuildContext context, string prefabPath, RuntimeAnimatorController conventionController)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            context.Report.Warning("Nao foi possivel abrir \"" + prefabPath + "\" para atribuir o Animator Controller.", context.Nome);
            return;
        }

        try
        {
            // GetComponentInChildren, nao um caminho fixo tipo "Pivot/<fbx>":
            // o Animator vive em lugares DIFERENTES conforme a estrutura
            // (Pivot/<modelo> no Personagem, <modelo> direto sob o root na
            // Torre, o proprio root no Monstro - ver comentario da classe).
            // Mesma filosofia "identidade, nao caminho literal" da Fase 6
            // (FindModelChild) - so que aqui buscando um TIPO de componente,
            // nao a identidade de um nested prefab.
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                context.Report.Warning("Prefab \"" + prefabPath + "\" nao tem nenhum componente Animator - nada para atribuir.", context.Nome);
                return;
            }

            if (animator.runtimeAnimatorController != null)
            {
                context.Report.Info(
                    "Prefab \"" + prefabPath + "\" ja tem Animator Controller atribuido (override de ExoPrefabProfile.animatorController, " +
                    "aplicado antes deste step) - convencao nao sobrescreve.",
                    context.Nome);
                return;
            }

            if (conventionController == null)
                return; // Warning global ja reportado em AssignAnimatorControllers - nao repete por prefab.

            animator.runtimeAnimatorController = conventionController;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            context.Report.Info("Animator Controller atribuido por convencao em \"" + prefabPath + "\".", context.Nome);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
