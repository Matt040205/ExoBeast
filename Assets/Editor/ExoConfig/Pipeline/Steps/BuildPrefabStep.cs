using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Embrulha a chamada atual para ExoPrefabBuilder.BuildCharacterPrefab. A
/// montagem de GameObjects/componentes em si continua fora deste step
/// (dentro de ExoPrefabBuilder) - so muda O QUE ele faz internamente:
///
/// Fase 7: deixou de ser o ultimo step do pipeline - AnimatorStep,
/// NetworkRegistrationStep e ValidateStep rodam depois dele (ver
/// ExoPrefabMenu.RunPipeline), todos dependendo de context.BuiltPrefabPaths
/// (populado abaixo, a partir do retorno de BuildCharacterPrefab - antes da
/// Fase 7 esse metodo devolvia void) para saber EXATAMENTE quais prefab(s)
/// esta execucao tocou.
///
/// Fase 5 trocou a metade de PERSONAGEM (ConfigureAsCharacter/
/// SetupCameraHierarchy, removidos - ver ExoPrefabBuilder.BuildOrUpdateCharacterVariant)
/// pela estrategia de Prefab Variant nativo (InstantiatePrefab(basePrefab) +
/// SaveAsPrefabAsset, ou LoadPrefabContents para update-in-place). A metade
/// de TORRE (ConfigureAsTower) e o Monstro (ConfigureAsEnemy) NAO mudaram -
/// continuam reconstruindo do zero e usando CopySerializedValuesAndRelink,
/// como antes da Fase 4 (ver Assets/Diretrizes_Multiagente.md).
///
/// Passa context.Report para BuildCharacterPrefab desde a Fase 5: e o unico
/// jeito de Warnings internos (ex.: ApplyAbilityScripts pulando um
/// MonoScript que nao resolve para Component valido, ou o Error de
/// basePrefab ausente) chegarem no relatorio estruturado do pipeline em vez
/// de so Debug.LogError/LogWarning soltos no console.
///
/// Em DryRun, NAO chama BuildCharacterPrefab: esse metodo grava prefabs/
/// materiais no disco incondicionalmente e nao tem nenhuma nocao de dry-run
/// (fora do escopo desta fase mudar isso) - este step so reporta o que
/// aconteceria.
///
/// Fase 5 tambem REMOVEU a checagem de InputActionAsset que este step fazia
/// aqui (Info/Warning via ExoInputActionsResolver, no Core): ela descrevia
/// especificamente a logica de fallback de ConfigureAsCharacter
/// (INPUT_ACTIONS_PATH_ALT/INPUT_ACTIONS_PATH), que foi removida junto com
/// ConfigureAsCharacter - PlayerInput.actions do Personagem agora vem
/// herdado de profile.basePrefab, nunca mais resolvido por
/// BuildCharacterPrefab. Manter aquela checagem aqui reportaria informacao
/// incorreta (poderia dizer "PlayerInput.actions ficara nulo" quando na
/// verdade ele vem do basePrefab, sempre). ExoInputActionsResolver (Core)
/// continua existindo e testado - so ficou sem chamador em codigo de
/// producao; nao foi apagado nesta fase por estar fora do escopo explicito
/// (ver briefing da Fase 5).
/// </summary>
public sealed class BuildPrefabStep : IExoBuildStep
{
    public string Name => "BuildPrefab";

    public void Execute(ExoBuildContext context)
    {
        if (context.DryRun)
        {
            context.Report.Info(
                "[DryRun] Prefab(s) de \"" + context.Nome + "\" seriam montados em \"" + context.PrefabsFolder + "\" (ExoPrefabBuilder.BuildCharacterPrefab).",
                context.Nome);
            return;
        }

        context.BuiltPrefabPaths = ExoPrefabBuilder.BuildCharacterPrefab(
            context.DestFbxPath, context.PrefabsFolder, context.MateriaisFolder, context.Profile, context.CategoriaRaw, context.Report);

        context.Report.Info("Prefab(s) de \"" + context.Nome + "\" montados em \"" + context.PrefabsFolder + "\".", context.Nome);
    }
}
