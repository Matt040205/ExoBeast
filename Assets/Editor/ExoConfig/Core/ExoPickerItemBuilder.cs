using System;
using System.Collections.Generic;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Um item pronto para exibicao no picker "Assets/Exo Prefabs/Organizar..."
    /// (Fase 3 da refatoracao - substitui os pares de [MenuItem] que
    /// Assets/Editor/ExoGeneratedMenus.cs gerava em disco, um por entidade;
    /// esse arquivo foi removido nesta fase).
    ///
    /// "Nome" e sempre o nome ORIGINAL, intacto, da entidade (acentos
    /// inclusive - ex.: "Águia", "Escorpião") - e o valor que precisa chegar
    /// sem nenhuma alteracao em ExoPrefabMenu.ExecutarOrganizar(categoria, nome),
    /// que usa esse nome para achar a entrada em ExoToolConfig
    /// (ExoToolConfig.FindEntry compara por igualdade ordinal exata).
    ///
    /// "MenuPath" e uma string SEPARADA, so para exibicao em
    /// UnityEditor.GenericMenu (montada aqui, embora este assembly nao
    /// referencie GenericMenu - e so concatenacao de string) - pode divergir
    /// de "Nome" quando o nome contiver '/' (ver
    /// ExoPickerItemBuilder.SanitizeMenuSegment).
    /// </summary>
    public readonly struct ExoPickerItem
    {
        public ExoCategory Categoria { get; }
        public string Nome { get; }
        public string MenuPath { get; }

        public ExoPickerItem(ExoCategory categoria, string nome, string menuPath)
        {
            Categoria = categoria;
            Nome = nome;
            MenuPath = menuPath;
        }
    }

    /// <summary>
    /// Monta, a partir de uma lista crua de ExoEntityDefinition (o que
    /// ExoToolConfig.Entries expoe via Select(e => e.Definition)), a lista
    /// ORDENADA e AGRUPADA por categoria de itens que o picker da Fase 3
    /// exibe.
    ///
    /// Substitui a geracao de codigo-fonte de Assets/Editor/ExoGeneratedMenus.cs
    /// (Fases 1-2): em vez de emitir um par de [MenuItem] por entidade em
    /// tempo de Editor e commitar o arquivo gerado (que podia divergir do
    /// gerador - ja aconteceu com o cabecalho "NAO EDITE"), o picker le esta
    /// lista em tempo de execucao a cada clique, direto do ExoToolConfig -
    /// sem arquivo intermediario, sem recompilacao a cada mudanca de config.
    ///
    /// Puro: so colecoes/string/enum, sem I/O, sem UnityEngine/UnityEditor
    /// (garantido em tempo de compilacao pelo noEngineReferences=true do
    /// asmdef deste assembly) - inclusive a montagem de MenuPath, que e so
    /// concatenacao/substituicao de string. UnityEditor.GenericMenu, que de
    /// fato INTERPRETA essa string como caminho de menu, so e referenciado
    /// fora deste assembly (Assets/Editor/ExoPrefabMenu.cs).
    /// </summary>
    public static class ExoPickerItemBuilder
    {
        /// <summary>
        /// UnityEditor.GenericMenu trata '/' (U+002F) no texto de um item
        /// como separador de submenu. Se o nome de uma entidade contiver
        /// '/', usar o nome cru em MenuPath quebraria a entidade num
        /// submenu extra indesejado em vez de aparecer como um unico
        /// item-folha. Nenhuma das 10 entidades reais de hoje
        /// (Assets/Editor/ExoConfig/ExoToolConfig.asset) tem '/' no nome,
        /// mas o picker nao deve quebrar (nem exibir estrutura errada) em
        /// silencio se uma futura entidade tiver.
        ///
        /// Tratamento: substitui '/' por U+2215 (DIVISION SLASH, "∕") so em
        /// MenuPath - visualmente quase identico a '/', mas GenericMenu so
        /// trata o caractere ASCII '/' como separador, entao U+2215 sempre
        /// aparece como parte do nome do item, nunca corta um submenu novo.
        /// "Nome" (o valor que chega em ExecutarOrganizar) nunca e alterado.
        /// </summary>
        private const char GenericMenuSeparator = '/';
        private const char GenericMenuSeparatorLookalike = '∕';

        /// <summary>
        /// Constroi a lista de itens do picker a partir de "entidades"
        /// (tipicamente ExoToolConfig.Entries.Select(e => e?.Definition)).
        ///
        /// Agrupamento: por ExoCategory, na ORDEM DE DECLARACAO do enum (via
        /// Enum.GetValues) - NAO na ordem em que aparecem em "entidades".
        /// Mesma tecnica de paridade ja usada por ExoCategoryParser/
        /// ExoConfigWindow.CATEGORIAS: garante ordem estavel independente de
        /// como as entidades foram cadastradas, e cobre categorias futuras
        /// automaticamente sem precisar editar este metodo.
        ///
        /// Ordenacao dentro de cada categoria: por Nome, via
        /// StringComparison.Ordinal - mesmo criterio ja usado pela opcao
        /// "A-Z" de ExoToolConfig.SortCategoria (botao "Organizar v" de
        /// ExoConfigWindow), entao o picker concorda com a janela sobre o
        /// que "A-Z" significa. Isso inclui a mesma consequencia de Ordinal
        /// que a janela ja tem: letras acentuadas maiusculas (ex.: 'Á')
        /// ordenam DEPOIS de todo o alfabeto ASCII maiusculo, entao "Águia"
        /// aparece por ultimo entre as entidades de Monstros, nao em ordem
        /// "alfabetica humana". Deliberadamente NAO corrigido aqui: mudar so
        /// no picker criaria uma segunda nocao divergente de "A-Z" dentro da
        /// mesma ferramenta.
        ///
        /// Tolerante a dados invalidos (mesma filosofia de
        /// ExoOverrideMapBuilder.Build): "entidades" nulo, elementos nulos,
        /// Nome nulo/vazio ou Categoria que nao bate com nenhum ExoCategory
        /// sao ignorados em silencio (nunca lancam excecao) e opcionalmente
        /// registrados em "report" como Warning.
        /// </summary>
        public static List<ExoPickerItem> BuildItems(IEnumerable<ExoEntityDefinition> entidades, ExoBuildReport report = null)
        {
            List<ExoPickerItem> resultado = new List<ExoPickerItem>();
            if (entidades == null)
                return resultado;

            Dictionary<ExoCategory, List<ExoEntityDefinition>> porCategoria = new Dictionary<ExoCategory, List<ExoEntityDefinition>>();

            foreach (ExoEntityDefinition definicao in entidades)
            {
                if (definicao == null || string.IsNullOrEmpty(definicao.Nome))
                    continue;

                if (!ExoCategoryParser.TryParse(definicao.Categoria, out ExoCategory categoria))
                {
                    report?.Warning(
                        "Categoria desconhecida \"" + definicao.Categoria + "\" - entidade nao aparece no picker.",
                        definicao.Nome);
                    continue;
                }

                if (!porCategoria.TryGetValue(categoria, out List<ExoEntityDefinition> grupo))
                {
                    grupo = new List<ExoEntityDefinition>();
                    porCategoria[categoria] = grupo;
                }
                grupo.Add(definicao);
            }

            foreach (ExoCategory categoria in (ExoCategory[])Enum.GetValues(typeof(ExoCategory)))
            {
                if (!porCategoria.TryGetValue(categoria, out List<ExoEntityDefinition> grupo))
                    continue;

                grupo.Sort((a, b) => string.Compare(a.Nome, b.Nome, StringComparison.Ordinal));

                foreach (ExoEntityDefinition definicao in grupo)
                {
                    string menuPath = categoria + "/" + SanitizeMenuSegment(definicao.Nome);
                    resultado.Add(new ExoPickerItem(categoria, definicao.Nome, menuPath));
                }
            }

            return resultado;
        }

        /// <summary>
        /// Substitui '/' pelo seu "sosia" visual U+2215 num segmento de nome,
        /// para uso seguro dentro de UnityEditor.GenericMenu (ver comentario
        /// em GenericMenuSeparatorLookalike acima). Publico porque e uma
        /// regra de nomenclatura pura e legitimamente testavel por si so, nao
        /// so um detalhe interno de BuildItems.
        ///
        /// Exige nome nao nulo/vazio (mesmo padrao de
        /// ExoNaming.RequireFbxName / ExoPathResolver.RequireNome) - BuildItems
        /// ja garante essa invariante antes de chamar este metodo.
        /// </summary>
        public static string SanitizeMenuSegment(string nome)
        {
            if (string.IsNullOrEmpty(nome))
                throw new ArgumentException("[ExoConfig] nome nao pode ser nulo ou vazio.", nameof(nome));

            return nome.Replace(GenericMenuSeparator, GenericMenuSeparatorLookalike);
        }
    }
}
