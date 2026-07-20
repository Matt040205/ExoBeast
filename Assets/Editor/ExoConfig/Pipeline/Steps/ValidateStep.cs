using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Fase 7 da refatoracao Exo Config - liga ExoFileIdPresenceChecker (Core,
/// criado na Fase 5 mas nunca usado por nenhum step ate agora) como guard de
/// verdade do pipeline.
///
/// O que valida: depois que um prefab de Personagem/Torre/Monstro e
/// montado/salvo, confirma que o fileID gravado no CharacterBase
/// (commanderPrefab/towerPrefab) ou EnemyDataSO (enemyPrefab) correspondente
/// aparece LITERALMENTE no YAML do prefab que acabou de ser salvo - a regra
/// duravel do projeto ("fileID de Prefab Variant tolerado no Editor, vira
/// null em build standalone") virando checagem automatica em vez de so
/// lembranca.
///
/// COMO EXTRAI O FILEID DO CAMPO DA SO (decisao desta fase, ver
/// ExoScriptableObjectReferenceParser para o raciocinio completo): le o
/// TEXTO SERIALIZADO do .asset da SO (File.ReadAllText) e faz parsing de
/// texto - NAO usa UnityEditor.Unsupported.GetLocalIdentifierInFile sobre o
/// GameObject live. Resumo da razao (detalhe completo no Core): perguntar ao
/// Editor "qual e o fileID deste objeto agora" arrisca reconstruir o MESMO
/// fileID "virtual" que o Editor tolera e que causa o bug em build - so ler
/// o BYTE serializado em disco (dos dois lados: da SO e do prefab) reproduz
/// fielmente o que uma build standalone realmente ve.
///
/// ERROR vs WARNING (decisao explicita desta fase, o briefing nao e
/// prescritivo aqui): este step usa Report.Warning, NUNCA Report.Error,
/// quando um fileID nao bate. Por que:
///   1. Este e o ULTIMO step do pipeline (ver ExoPrefabMenu.RunPipeline) -
///      quando ele roda, o FBX ja foi movido, o material ja foi criado/
///      atualizado, o(s) prefab(s) ja foram montados/salvos, Animator e
///      registro de rede ja rodaram. Um Report.Error aqui NAO desfaz nada
///      disso (o pipeline nao tem rollback) - so pinta a execucao inteira de
///      vermelho por um problema que, na pior hipotese, afeta UMA referencia
///      especifica, nao a organizacao/montagem como um todo.
///   2. Precedente direto no proprio ExoPrefabBuilder: o cenario mais
///      parecido que ja existe no codigo hoje - FindOriginalPrefab caindo no
///      fallback fuzzy (Contains) e relinkando contra o prefab ERRADO em
///      potencial - usa Debug.LogWarning, nao Error, mesmo sendo um risco de
///      dado silenciosamente incorreto (arguivelmente pior que um null
///      obvio). "Rig references podem ter ficado nulas apos troca de
///      modelo" (BuildOrUpdateCharacterVariant) tambem e Warning. Um
///      Report.Error aqui quebraria a consistencia com essas duas decisoes
///      ja tomadas para riscos de severidade comparavel.
///   3. Falso positivo plausivel e ESPERADO, nao excepcional: characterData/
///      enemyData nao configurado ainda e o estado REAL de todas as 4
///      entidades de Personagem hoje (Ayame/Brunhilde/Coral/Sylvie sem
///      profile algum, confirmado na Fase 5) - se essa checagem fosse Error,
///      o primeiro build de QUALQUER entidade nova (antes do designer ligar
///      o CharacterBase/EnemyDataSO no profile) travaria o pipeline por um
///      estado intermediario normal. Este step trata "SO nao configurada"
///      como Info (nada para validar), nunca Warning/Error - mas a mesma
///      tolerancia a estados intermediarios pede cautela extra antes de user
///      Error para o caso "configurada mas nao bate" tambem.
/// Warning e suficiente para o proposito real: colocar o alerta no relatorio
/// estruturado (visivel no console, com contexto - nome da entidade, campo,
/// fileID, caminho) para o designer investigar antes de publicar uma build,
/// sem bloquear o fluxo de trabalho normal de organizar/iterar no Editor.
///
/// Escopo por EntityType (nao Categoria - a mesma distincao que
/// BuildCharacterPrefab usa internamente para decidir Personagem vs
/// Monstro/Edificio, ver ResolvePathsStep):
///   - Personagem: valida characterData.commanderPrefab contra
///     BuiltPrefabPaths[0] E characterData.towerPrefab contra
///     BuiltPrefabPaths[1] (contrato de ordem documentado em
///     ExoBuildContext.BuiltPrefabPaths).
///   - Monstro: valida enemyData.enemyPrefab contra BuiltPrefabPaths[0].
///   - Edificio: nada a validar (ConfigureAsBuilding nao vincula nenhuma SO -
///     confirmado via leitura de ExoPrefabBuilder nesta fase).
/// </summary>
public sealed class ValidateStep : IExoBuildStep
{
    public string Name => "Validate";

    public void Execute(ExoBuildContext context)
    {
        if (context.DryRun)
        {
            context.Report.Info("[DryRun] Nenhum prefab foi montado - validacao de fileID pulada.", context.Nome);
            return;
        }

        if (context.Profile == null)
        {
            context.Report.Info("Sem ExoPrefabProfile - nada para validar.", context.Nome);
            return;
        }

        switch (context.EntityType)
        {
            case ExoEntityType.Personagem:
                ValidatePersonagem(context);
                break;
            case ExoEntityType.Monstro:
                ValidateMonstro(context);
                break;
            default:
                context.Report.Info("Edificio nao vincula nenhuma referencia de ScriptableObject - nada para validar.", context.Nome);
                break;
        }
    }

    private static void ValidatePersonagem(ExoBuildContext context)
    {
        if (context.Profile.characterData == null)
        {
            context.Report.Info("ExoPrefabProfile.characterData nao configurado - validacao de fileID pulada.", context.Nome);
            return;
        }

        // Contrato de ordem: ver ExoBuildContext.BuiltPrefabPaths.
        string characterPath = context.BuiltPrefabPaths.Count > 0 ? context.BuiltPrefabPaths[0] : null;
        string towerPath = context.BuiltPrefabPaths.Count > 1 ? context.BuiltPrefabPaths[1] : null;

        ValidateReference(context, context.Profile.characterData, "commanderPrefab", characterPath);
        ValidateReference(context, context.Profile.characterData, "towerPrefab", towerPath);
    }

    private static void ValidateMonstro(ExoBuildContext context)
    {
        if (context.Profile.enemyData == null)
        {
            context.Report.Info("ExoPrefabProfile.enemyData nao configurado - validacao de fileID pulada.", context.Nome);
            return;
        }

        string enemyPath = context.BuiltPrefabPaths.Count > 0 ? context.BuiltPrefabPaths[0] : null;
        ValidateReference(context, context.Profile.enemyData, "enemyPrefab", enemyPath);
    }

    /// <summary>
    /// Confirma que o fileID gravado em "scriptableObject.fieldName" aparece
    /// literalmente no YAML do prefab salvo em "builtPrefabPath". Ve
    /// comentario da classe para Error-vs-Warning e para a escolha de ler
    /// texto serializado em vez de GetLocalIdentifierInFile.
    /// </summary>
    private static void ValidateReference(ExoBuildContext context, UnityEngine.Object scriptableObject, string fieldName, string builtPrefabPath)
    {
        string soPath = AssetDatabase.GetAssetPath(scriptableObject);
        string soYaml = ReadAssetText(soPath);
        if (soYaml == null)
        {
            context.Report.Warning("Nao foi possivel ler \"" + soPath + "\" (" + scriptableObject.name + ") para validar \"" + fieldName + "\".", context.Nome);
            return;
        }

        string fileId = ExoScriptableObjectReferenceParser.ExtractFileId(soYaml, fieldName);
        if (fileId == null)
        {
            context.Report.Info("\"" + scriptableObject.name + "." + fieldName + "\" nao aponta para nenhum prefab - nada para validar.", context.Nome);
            return;
        }

        if (string.IsNullOrEmpty(builtPrefabPath))
        {
            context.Report.Warning(
                "\"" + scriptableObject.name + "." + fieldName + "\" aponta para fileID " + fileId + ", mas nenhum prefab correspondente foi montado " +
                "nesta execucao para confirmar (ver ExoBuildContext.BuiltPrefabPaths).",
                context.Nome);
            return;
        }

        // Confere o GUID ANTES do fileID (achado real desta fase, nao
        // hipotese): fileIDs "bem conhecidos" que a Unity atribui por
        // convencao ao objeto principal de um modelo importado se repetem
        // entre GUIDs DIFERENTES com frequencia real (confirmado: o fileID
        // 919132149155446097 aparece como raiz de modelo em pelo menos dois
        // FBX distintos deste projeto). Sem esta checagem,
        // ExoFileIdPresenceChecker.ContainsFileId (que so olha o numero do
        // fileID, nunca o guid) confirmaria "sim" para uma referencia
        // apontando para um asset COMPLETAMENTE ERRADO, so por coincidencia
        // de fileID - falso positivo pior que nao validar nada. So bloqueia
        // quando os dois guids sao conhecidos E diferentes; guid ausente de
        // um dos lados (ex.: formato de YAML inesperado) degrada para a
        // checagem de fileID de qualquer forma, sem lancar excecao.
        string expectedGuid = AssetDatabase.AssetPathToGUID(builtPrefabPath);
        string referenceGuid = ExoScriptableObjectReferenceParser.ExtractGuid(soYaml, fieldName);
        if (!string.IsNullOrEmpty(referenceGuid) && !string.IsNullOrEmpty(expectedGuid)
            && !string.Equals(referenceGuid, expectedGuid, StringComparison.OrdinalIgnoreCase))
        {
            context.Report.Warning(
                "\"" + scriptableObject.name + "." + fieldName + "\" aponta para um asset DIFERENTE (guid " + referenceGuid + ") do prefab montado " +
                "nesta execucao (\"" + builtPrefabPath + "\", guid " + expectedGuid + "). Referencia provavelmente errada - confira manualmente.",
                context.Nome);
            return;
        }

        string prefabYaml = ReadAssetText(builtPrefabPath);
        if (prefabYaml == null)
        {
            context.Report.Warning("Nao foi possivel ler \"" + builtPrefabPath + "\" para validar \"" + fieldName + "\".", context.Nome);
            return;
        }

        bool found = ExoFileIdPresenceChecker.ContainsFileId(prefabYaml, fileId);
        if (found)
        {
            context.Report.Info(
                "\"" + scriptableObject.name + "." + fieldName + "\" (fileID " + fileId + ") confirmado no YAML de \"" + builtPrefabPath + "\".",
                context.Nome);
        }
        else
        {
            context.Report.Warning(
                "\"" + scriptableObject.name + "." + fieldName + "\" referencia fileID " + fileId + ", que NAO aparece no YAML de \"" + builtPrefabPath + "\". " +
                "Essa referencia pode resolver como null numa build standalone (regra duravel do projeto - fileID de Prefab Variant tolerado no Editor mas " +
                "nao serializado literalmente, ver ExoFileIdPresenceChecker). Reabra o Inspector de \"" + scriptableObject.name + "\" e reatribua \"" + fieldName + "\" manualmente.",
                context.Nome);
        }
    }

    /// <summary>
    /// Le o conteudo de um asset "Assets/..." como texto puro (nao via
    /// AssetDatabase - precisamos do BYTE SERIALIZADO, nao de um objeto
    /// deserializado/live - ver comentario da classe). Devolve null (nunca
    /// lanca) se o caminho for vazio, nao comecar com "Assets" (nao e um
    /// caminho de asset valido) ou o arquivo nao existir no disco.
    /// </summary>
    private static string ReadAssetText(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets", StringComparison.Ordinal))
            return null;

        string fullPath = ExoPathResolver.Normalize(Application.dataPath + assetPath.Substring("Assets".Length));
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
    }
}
