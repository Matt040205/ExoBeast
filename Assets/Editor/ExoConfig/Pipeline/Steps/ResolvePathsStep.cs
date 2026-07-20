using System;
using UnityEditor;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Primeiro step do pipeline: valida (categoria, nome) contra ExoToolConfig e
/// resolve as 4 pastas de destino (Modelos/Texturas/Prefabs/Materiais), o
/// ExoPrefabProfile vinculado (se houver) e o ExoEntityType efetivo da
/// entidade.
///
/// Espelha as duas guard clauses que existiam no topo de
/// ExoPrefabMenu.ExecutarOrganizar antes da Fase 4 (categoria desconhecida /
/// entidade nao cadastrada), so que reportando via context.Report.Error em
/// vez de Debug.LogError + return direto - o pipeline para sozinho no
/// primeiro erro (ver ExoBuildPipeline.Run), entao nenhum step seguinte
/// (ImportAssetsStep, MaterialStep, BuildPrefabStep) roda se este falhar.
/// </summary>
public sealed class ResolvePathsStep : IExoBuildStep
{
    public string Name => "ResolvePaths";

    public void Execute(ExoBuildContext context)
    {
        if (!ExoCategoryParser.TryParse(context.CategoriaRaw, out ExoCategory categoria))
        {
            context.Report.Error(
                "Categoria \"" + context.CategoriaRaw + "\" desconhecida. Verifique Assets/Editor/ExoConfig/ExoToolConfig.asset (menu Exo Config > Edit).",
                context.Nome);
            return;
        }
        context.Categoria = categoria;

        ExoToolConfig config = ExoToolConfig.Load();
        ExoToolConfigEntry entry = config?.FindEntry(categoria, context.Nome);
        if (config == null || entry == null)
        {
            context.Report.Error(
                "Entidade \"" + context.Nome + "\" (" + context.CategoriaRaw + ") nao encontrada em ExoToolConfig. Verifique o Exo Config.",
                context.Nome);
            return;
        }
        context.Config = config;
        context.Entry = entry;

        context.Profile = string.IsNullOrEmpty(entry.ProfileAssetPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(entry.ProfileAssetPath);

        // Mesma regra de ExoPrefabBuilder.BuildCharacterPrefab (linhas
        // ~45-49) e de ExoConfigWindow.DrawProfileSection (botao "Criar
        // Perfil"): perfil vence a convencao de categoria quando presente.
        // Duplicada aqui (nao extraida para um unico ponto compartilhado)
        // porque ExoEntityType vive fora do assembly
        // ExoBeasts.ExoConfig.Core (definido em ExoPrefabProfile.cs, sem
        // asmdef) e BuildCharacterPrefab nao pode mudar nesta fase (ver
        // escopo da Fase 4 - ConfigureAsCharacter/ConfigureAsTower/
        // ConfigureAsEnemy/CopySerializedValuesAndRelink ficam intocados) -
        // entao a copia dentro dele fica como estava, e esta e a unica outra
        // copia nova introduzida pelo pipeline (usada por MaterialStep).
        context.EntityType = categoria == ExoCategory.Monstros ? ExoEntityType.Monstro
                            : categoria == ExoCategory.Environment ? ExoEntityType.Edificio
                            : ExoEntityType.Personagem;
        if (context.Profile != null)
            context.EntityType = context.Profile.entityType;

        try
        {
            context.ModelosFolder = config.ResolveFolder(categoria, context.Nome, ExoAssetType.Modelos, context.Report);
            context.TexturasFolder = config.ResolveFolder(categoria, context.Nome, ExoAssetType.Texturas, context.Report);
            context.PrefabsFolder = config.ResolveFolder(categoria, context.Nome, ExoAssetType.Prefabs, context.Report);
            context.MateriaisFolder = config.ResolveFolder(categoria, context.Nome, ExoAssetType.Materiais, context.Report);

            // Fase 7 (AnimatorStep): Animacao so se aplica a Personagens/
            // Monstros (ExoPathResolver.SupportsAssetType - Environment nao
            // tem essa subpasta na convencao real do projeto). Ao contrario
            // dos 4 tipos acima, NAO chamamos ResolveFolder incondicionalmente
            // aqui: ResolveFolder lanca InvalidOperationException para um
            // tipo que a categoria nao suporta (ver ExoPathResolver.ResolveFolder),
            // e isso cairia no catch abaixo como um Report.Error - errado
            // para Environment, onde "nao ter pasta de Animacao" e esperado,
            // nao uma falha. Guard explicito em vez de depender do catch.
            context.AnimacaoFolder = ExoPathResolver.SupportsAssetType(categoria, ExoAssetType.Animacao)
                ? config.ResolveFolder(categoria, context.Nome, ExoAssetType.Animacao, context.Report)
                : null;
        }
        catch (Exception ex)
        {
            context.Report.Error("Falha ao resolver pastas de " + context.Nome + ": " + ex.Message, context.Nome);
        }
    }
}
