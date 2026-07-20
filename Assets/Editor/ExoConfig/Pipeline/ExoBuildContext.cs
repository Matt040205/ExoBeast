using System.IO;
using UnityEngine;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Estado compartilhado de uma execucao do pipeline de organizacao/montagem
/// de prefab (Fase 4 da refatoracao Exo Config). Cada IExoBuildStep le e
/// escreve campos deste contexto - ver ExoBuildPipeline para a orquestracao
/// dos steps e Assets/Editor/ExoConfig/Pipeline/Steps/ para os steps desta
/// fase (ResolvePathsStep, ImportAssetsStep, MaterialStep, BuildPrefabStep).
///
/// Fica fora do assembly ExoBeasts.ExoConfig.Core de proposito (mesma razao
/// de ExoToolConfig/ExoPrefabProfile): guarda tipos de UnityEngine
/// (ExoPrefabProfile, Material) que o Core nao pode referenciar
/// (noEngineReferences=true no asmdef do Core, garantido em tempo de
/// compilacao). Sem asmdef proprio - compila em Assembly-CSharp-Editor,
/// junto de ExoPrefabMenu/ExoPrefabBuilder/ExoToolConfig - porque
/// BuildPrefabStep precisa chamar ExoPrefabBuilder, que referencia tipos de
/// jogo (PlayerMovement, TowerController, CharacterBase, etc.) e asmdefs nao
/// podem referenciar Assembly-CSharp (ver Assets/Diretrizes_Multiagente.md e
/// o briefing desta fase).
/// </summary>
public sealed class ExoBuildContext
{
    /// <summary>Categoria como recebida por ExoPrefabMenu.ExecutarOrganizar - string crua, antes de validar contra ExoCategory. Ver ResolvePathsStep.</summary>
    public string CategoriaRaw { get; }

    /// <summary>Nome da entidade, exatamente como cadastrado em ExoToolConfig (acentos inclusos).</summary>
    public string Nome { get; }

    /// <summary>Caminho do asset FBX selecionado no Project (Selection.activeObject), antes de mover.</summary>
    public string SourceFbxPath { get; }

    /// <summary>Nome do arquivo do FBX de origem, sem pasta nem extensao (ex.: "samurai 3").</summary>
    public string FbxFileName { get; }

    /// <summary>Pasta que contem o FBX de origem - onde ImportAssetsStep procura a textura irma "[Nome]T.png".</summary>
    public string SourceFolderPath { get; }

    /// <summary>Se true, nenhum step grava no disco (nem move asset, nem cria pasta, nem cria/atualiza material ou prefab) - so popula Report com o que faria.</summary>
    public bool DryRun { get; }

    /// <summary>Relatorio estruturado acumulado por todos os steps - nunca Debug.*, ver ExoBuildReport (Core).</summary>
    public ExoBuildReport Report { get; }

    // --- Preenchido por ResolvePathsStep ---
    public ExoCategory Categoria { get; set; }
    public ExoEntityType EntityType { get; set; }
    public ExoToolConfig Config { get; set; }
    public ExoToolConfigEntry Entry { get; set; }
    public ExoPrefabProfile Profile { get; set; }
    public string ModelosFolder { get; set; }
    public string TexturasFolder { get; set; }
    public string PrefabsFolder { get; set; }
    public string MateriaisFolder { get; set; }

    // --- Preenchido por ImportAssetsStep ---
    /// <summary>Caminho final do FBX depois de movido (ou o caminho que TERIA em DryRun). Null ate ImportAssetsStep rodar.</summary>
    public string DestFbxPath { get; set; }

    /// <summary>Caminho final da textura depois de movida. Null se nao havia textura irma para mover (nao e erro - ver ImportAssetsStep).</summary>
    public string DestTexturePath { get; set; }

    // --- Preenchido por MaterialStep ---
    /// <summary>Material criado/atualizado. Null em DryRun (nenhum material real e tocado) ou se o shader nao foi encontrado (MaterialStep ja reporta Error nesse caso).</summary>
    public Material Material { get; set; }

    public ExoBuildContext(string categoriaRaw, string nome, string sourceFbxPath, bool dryRun, ExoBuildReport report = null)
    {
        CategoriaRaw = categoriaRaw;
        Nome = nome;
        SourceFbxPath = sourceFbxPath;
        FbxFileName = Path.GetFileNameWithoutExtension(sourceFbxPath);
        SourceFolderPath = ExoPathResolver.Normalize(Path.GetDirectoryName(sourceFbxPath));
        DryRun = dryRun;
        Report = report ?? new ExoBuildReport();
    }
}
