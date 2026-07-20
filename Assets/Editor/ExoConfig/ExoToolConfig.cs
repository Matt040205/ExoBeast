using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// ScriptableObject versionado que substitui o EditorPrefs como fonte de
/// verdade da ferramenta Exo Config (Fase 2 da refatoracao - ver
/// Assets/Editor/ExoConfig/Core para a logica pura reaproveitada aqui).
///
/// Guarda a lista de entidades (nome + categoria) e os overrides de pasta
/// por entidade/tipo - exatamente o que estava espalhado em chaves soltas do
/// EditorPrefs (chave de lista = nome da categoria; caminhos =
/// "{Categoria}_{Nome}_{Mod|Tex|Pre|Mat|Ani}"). Tambem guarda, por entidade,
/// duas informacoes de bookkeeping do editor que tambem viviam no EditorPrefs
/// e sao necessarias para a janela continuar funcional sem deixar nenhuma
/// leitura/escrita de EditorPrefs para tras (objetivo da Fase 2 - "Nenhum
/// EditorPrefs novo. O objetivo e remove-lo, nao move-lo"): o asset path do
/// ExoPrefabProfile vinculado (era "{Categoria}_{Nome}_Profile") e os
/// timestamps de criacao/modificacao que alimentam o menu "Organizar" da
/// janela (eram "Created_{Categoria}_{Nome}"/"Modified_{Categoria}_{Nome}").
/// Ver ExoToolConfigEntry no fim deste arquivo para por que esses dois campos
/// NAO entraram em ExoEntityDefinition (Core).
///
/// NAO guarda os 5 caminhos de pasta "convencionais" por entidade - esses sao
/// sempre derivados via ExoPathResolver.ResolveFolder(categoria, nome, tipo,
/// overrides) a partir do nome/categoria; overrides so entram quando uma
/// entidade tiver uma pasta explicitamente diferente da convencao.
///
/// Fica fora do assembly ExoBeasts.ExoConfig.Core de proposito: ScriptableObject
/// exige UnityEngine, e o asmdef do Core tem noEngineReferences=true (garante
/// em tempo de compilacao que o Core nunca ganha essa dependencia). Este
/// arquivo compila em Assembly-CSharp-Editor (nao ha .asmdef proprio em
/// Assets/Editor/, e o Core tem autoReferenced=true, entao a referencia ao
/// namespace ExoBeasts.ExoConfig.Core acontece automaticamente).
/// </summary>
public class ExoToolConfig : ScriptableObject
{
    /// <summary>
    /// Caminho fixo do asset unico desta ferramenta.
    ///
    /// Por que aqui (Assets/Editor/ExoConfig/, junto do proprio script, ao
    /// lado da pasta Core/): (1) tem que ser versionado no git como qualquer
    /// outro asset do projeto - um .asset dentro de Assets/ e serializado
    /// como YAML texto pela Unity, entao entra no git normalmente e da pra
    /// revisar diff; (2) "Editor/" e nome de pasta especial reconhecido pela
    /// Unity - qualquer coisa dentro dela e excluida automaticamente de
    /// builds de player, que e exatamente o que se quer para configuracao de
    /// uma ferramenta que so existe dentro do Editor; (3) caminho FIXO e
    /// conhecido em tempo de compilacao significa que da pra carregar com
    /// AssetDatabase.LoadAssetAtPath direto (ver Load() abaixo) - sem
    /// ambiguidade de "e se existir mais de um ExoToolConfig no projeto"
    /// que um AssetDatabase.FindAssets("t:ExoToolConfig") teria, e sem
    /// precisar de pasta Resources (que so faria sentido se algo fora do
    /// Editor precisasse carregar isso em runtime de player - nao e o caso,
    /// a ferramenta inteira e EditorWindow/AssetDatabase/MenuItem).
    /// </summary>
    public const string AssetPath = "Assets/Editor/ExoConfig/ExoToolConfig.asset";

    [SerializeField]
    private List<ExoToolConfigEntry> entries = new List<ExoToolConfigEntry>();

    /// <summary>
    /// Todas as entidades cadastradas, de todas as categorias. Somente
    /// leitura pelo lado de fora - use AddEntity/RemoveEntity/
    /// SetFolderOverride/ClearFolderOverride/SetProfileAssetPath para mudar
    /// (elas cuidam de marcar o asset como sujo e salvar).
    /// </summary>
    public IReadOnlyList<ExoToolConfigEntry> Entries => entries;

    // ---------------------------------------------------------------
    // Carregamento
    // ---------------------------------------------------------------

    /// <summary>
    /// Carrega o asset em AssetPath. Devolve null se ainda nao existir - NAO
    /// cria como efeito colateral. Uso: superficies read-only que podem ser
    /// chamadas em contextos inesperados (ex.: ExoPrefabMenu.BuildPickerItems,
    /// que le a config toda vez que o usuario abre o picker "Assets/Exo
    /// Prefabs/Organizar...") e nao devem ter o efeito colateral surpresa de
    /// criar um asset novo no disco so por terem sido invocadas.
    /// </summary>
    public static ExoToolConfig Load()
    {
        return AssetDatabase.LoadAssetAtPath<ExoToolConfig>(AssetPath);
    }

    /// <summary>
    /// Carrega o asset em AssetPath, criando um novo (vazio) e salvando se
    /// ainda nao existir. Uso: superficies que existem para EDITAR a config
    /// (ExoConfigWindow, o migrador de EditorPrefs) - essas sempre precisam
    /// de algo para editar, mesmo em um clone novo do repositorio onde por
    /// algum motivo o .asset nao tenha vindo (nao deveria acontecer, ja que e
    /// versionado, mas isso evita a janela quebrar nesse cenario).
    /// </summary>
    public static ExoToolConfig LoadOrCreate()
    {
        ExoToolConfig existente = Load();
        if (existente != null)
            return existente;

        string pastaDoAsset = Path.GetDirectoryName(AssetPath);
        if (!string.IsNullOrEmpty(pastaDoAsset))
        {
            pastaDoAsset = pastaDoAsset.Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(pastaDoAsset))
            {
                Directory.CreateDirectory(pastaDoAsset);
                AssetDatabase.Refresh();
            }
        }

        ExoToolConfig criado = CreateInstance<ExoToolConfig>();
        AssetDatabase.CreateAsset(criado, AssetPath);
        AssetDatabase.SaveAssets();
        return criado;
    }

    // ---------------------------------------------------------------
    // Consulta
    // ---------------------------------------------------------------

    public ExoToolConfigEntry FindEntry(ExoCategory categoria, string nome)
    {
        if (string.IsNullOrEmpty(nome))
            return null;

        string categoriaStr = categoria.ToString();
        for (int i = 0; i < entries.Count; i++)
        {
            ExoToolConfigEntry entry = entries[i];
            if (entry?.Definition == null)
                continue;

            if (string.Equals(entry.Definition.Categoria, categoriaStr, StringComparison.Ordinal)
                && string.Equals(entry.Definition.Nome, nome, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    public IEnumerable<ExoToolConfigEntry> GetByCategoria(ExoCategory categoria)
    {
        string categoriaStr = categoria.ToString();
        return entries.Where(e => e?.Definition != null
            && string.Equals(e.Definition.Categoria, categoriaStr, StringComparison.Ordinal));
    }

    /// <summary>
    /// Reordena, IN PLACE, so as entradas de "categoria" segundo
    /// "comparison" - usado pelo menu "Organizar" de ExoConfigWindow (A-Z /
    /// Data Criacao / Data Modificacao), equivalente ao antigo
    /// List&lt;string&gt;.Sort(comparison) + SaveList sobre a chave CSV do
    /// EditorPrefs.
    ///
    /// Implementado como "extrai o grupo da categoria, ordena, reinsere no
    /// fim" em vez de um Sort posicional in-place sobre a lista inteira: como
    /// "entries" e uma lista unica compartilhada por todas as categorias (nao
    /// uma lista por categoria), e a UI so exibe uma categoria de cada vez
    /// via GetByCategoria, a ordem RELATIVA entre categorias diferentes nunca
    /// e observavel - so a ordem dentro de uma mesma categoria importa. Isso
    /// simplifica a implementacao sem mudar nenhum comportamento visivel.
    /// </summary>
    public void SortCategoria(ExoCategory categoria, Comparison<ExoToolConfigEntry> comparison)
    {
        if (comparison == null)
            throw new ArgumentNullException(nameof(comparison));

        List<ExoToolConfigEntry> grupo = GetByCategoria(categoria).ToList();
        if (grupo.Count < 2)
            return;

        grupo.Sort(comparison);

        string categoriaStr = categoria.ToString();
        entries.RemoveAll(e => e?.Definition != null
            && string.Equals(e.Definition.Categoria, categoriaStr, StringComparison.Ordinal));
        entries.AddRange(grupo);

        MarkDirty();
    }

    /// <summary>
    /// Overrides de pasta de TODAS as entidades cadastradas, no formato que
    /// ExoPathResolver.ResolveFolder espera. Reconstroi o dicionario a cada
    /// chamada a partir de "entries" - custo irrelevante (dezenas de
    /// entidades no maximo) para uma janela de Editor que ja redesenha tudo a
    /// cada frame do IMGUI; nao ha necessidade de cache, e cache introduziria
    /// risco de ficar desatualizado apos uma mutacao.
    /// </summary>
    public IReadOnlyDictionary<ExoPathOverrideKey, string> GetOverrides(ExoBuildReport report = null)
    {
        return ExoOverrideMapBuilder.Build(entries.Where(e => e != null).Select(e => e.Definition), report);
    }

    /// <summary>
    /// Atalho para ExoPathResolver.ResolveFolder ja injetando os overrides
    /// desta config - a API de leitura que a Fase 2 pede explicitamente.
    /// </summary>
    public string ResolveFolder(ExoCategory categoria, string nome, ExoAssetType tipo, ExoBuildReport report = null)
    {
        return ExoPathResolver.ResolveFolder(categoria, nome, tipo, GetOverrides(report));
    }

    // ---------------------------------------------------------------
    // Mutacao (usadas por ExoConfigWindow e pelo migrador de EditorPrefs)
    // ---------------------------------------------------------------

    /// <summary>
    /// Adiciona uma entidade nova. Idempotente: se (categoria, nome) ja
    /// existe, devolve a entrada existente sem duplicar nem tocar em nada
    /// (mesma semantica de "AddEntity" ser seguro de chamar mais de uma vez -
    /// usado pelo migrador, que pode rodar sobre uma config ja semeada).
    /// </summary>
    public ExoToolConfigEntry AddEntity(ExoCategory categoria, string nome)
    {
        if (string.IsNullOrEmpty(nome))
            throw new ArgumentException("[ExoConfig] nome da entidade nao pode ser vazio.", nameof(nome));

        ExoToolConfigEntry existente = FindEntry(categoria, nome);
        if (existente != null)
            return existente;

        long agora = DateTime.Now.Ticks;
        ExoToolConfigEntry novo = new ExoToolConfigEntry
        {
            Definition = new ExoEntityDefinition { Nome = nome, Categoria = categoria.ToString() },
            ProfileAssetPath = string.Empty,
            CreatedTicks = agora,
            ModifiedTicks = agora
        };
        entries.Add(novo);
        MarkDirty();
        return novo;
    }

    /// <summary>
    /// Remove a entidade (e, junto, todos os overrides/perfil/timestamps
    /// dela - sao parte do mesmo objeto ExoToolConfigEntry). Isso e uma
    /// melhoria incidental sobre o comportamento original: o botao "X" do
    /// EditorPrefs so removia o nome da lista CSV, deixando as chaves
    /// "{Categoria}_{Nome}_*" orfas no registro do Windows para sempre. Com
    /// um objeto por entidade em vez de chaves soltas, remover a entidade
    /// remove tudo por construcao - nao precisa de nenhum codigo extra de
    /// limpeza.
    /// </summary>
    public bool RemoveEntity(ExoCategory categoria, string nome)
    {
        ExoToolConfigEntry existente = FindEntry(categoria, nome);
        if (existente == null)
            return false;

        entries.Remove(existente);
        MarkDirty();
        return true;
    }

    /// <summary>
    /// Define (cria ou substitui) o override de pasta de um tipo especifico.
    /// "pasta" nula/vazia e tratada como "remover o override" (delega para
    /// ClearFolderOverride) em vez de gravar uma entrada inutil que
    /// ExoPathResolver.ResolveFolder ja ignoraria mesmo assim (ele so usa o
    /// override se nao for nulo/vazio) - evita lixo silencioso no asset.
    /// </summary>
    public void SetFolderOverride(ExoCategory categoria, string nome, ExoAssetType tipo, string pasta)
    {
        if (string.IsNullOrEmpty(pasta))
        {
            ClearFolderOverride(categoria, nome, tipo);
            return;
        }

        ExoToolConfigEntry entry = FindEntry(categoria, nome);
        if (entry == null)
            throw new InvalidOperationException("[ExoConfig] Entidade nao cadastrada: " + categoria + "/" + nome);

        string tipoStr = tipo.ToString();
        ExoFolderOverride existente = entry.Definition.FolderOverrides
            .FirstOrDefault(o => string.Equals(o.Tipo, tipoStr, StringComparison.Ordinal));

        if (existente != null)
            existente.Pasta = pasta;
        else
            entry.Definition.FolderOverrides.Add(new ExoFolderOverride(tipoStr, pasta));

        entry.ModifiedTicks = DateTime.Now.Ticks;
        MarkDirty();
    }

    public void ClearFolderOverride(ExoCategory categoria, string nome, ExoAssetType tipo)
    {
        ExoToolConfigEntry entry = FindEntry(categoria, nome);
        if (entry == null)
            return;

        string tipoStr = tipo.ToString();
        int removidos = entry.Definition.FolderOverrides.RemoveAll(o => string.Equals(o.Tipo, tipoStr, StringComparison.Ordinal));
        if (removidos > 0)
        {
            entry.ModifiedTicks = DateTime.Now.Ticks;
            MarkDirty();
        }
    }

    public void SetProfileAssetPath(ExoCategory categoria, string nome, string assetPath)
    {
        ExoToolConfigEntry entry = FindEntry(categoria, nome);
        if (entry == null)
            return;

        entry.ProfileAssetPath = assetPath ?? string.Empty;
        entry.ModifiedTicks = DateTime.Now.Ticks;
        MarkDirty();
    }

    private void MarkDirty()
    {
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssetIfDirty(this);
    }
}

/// <summary>
/// Uma entrada de ExoToolConfig: compoe a ExoEntityDefinition "pura" do Core
/// (Nome/Categoria/FolderOverrides) com bookkeeping de editor que nao
/// pertence ao Core por ser especifico da ferramenta/UI, nao do dominio de
/// resolucao de caminho - o asset path do ExoPrefabProfile vinculado e os
/// timestamps de criacao/modificacao que alimentam o menu "Organizar" (A-Z /
/// Data Criacao / Data Modificacao) de ExoConfigWindow.
///
/// Ficou como composicao (tem um Definition) em vez de herdar de
/// ExoEntityDefinition de proposito: evita misturar "e uma definicao pura de
/// entidade" (Core, Fase 1, ja testada) com "e uma entrada de config do
/// editor com metadados extras" (Fase 2) na mesma hierarquia de tipos, e
/// evita adicionar ao Core campos que so fazem sentido dentro do Editor.
/// </summary>
[Serializable]
public class ExoToolConfigEntry
{
    public ExoEntityDefinition Definition = new ExoEntityDefinition();
    public string ProfileAssetPath = string.Empty;
    public long CreatedTicks;
    public long ModifiedTicks;

    /// <summary>
    /// Verifica se ha override de pasta para "tipo" nesta entrada. Compara
    /// via ExoAssetTypeParser (nao um "==" direto de string) pela mesma razao
    /// de ExoOverrideMapBuilder: FolderOverrides[].Tipo e dado cru/tolerante,
    /// entao a conversao passa sempre pelo ponto canonico do Core.
    /// </summary>
    public bool TryGetFolderOverride(ExoAssetType tipo, out string pasta)
    {
        if (Definition?.FolderOverrides != null)
        {
            foreach (ExoFolderOverride overr in Definition.FolderOverrides)
            {
                if (overr == null)
                    continue;

                if (ExoAssetTypeParser.TryParse(overr.Tipo, out ExoAssetType tipoParseado) && tipoParseado == tipo)
                {
                    pasta = overr.Pasta;
                    return true;
                }
            }
        }

        pasta = null;
        return false;
    }
}
