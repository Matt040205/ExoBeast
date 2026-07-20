using System.Collections.Generic;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Converte uma lista de ExoEntityDefinition (dados crus, tolerantes -
    /// Categoria e FolderOverrides[].Tipo como string) no dicionario
    /// fortemente tipado que ExoPathResolver.ResolveFolder espera
    /// (IReadOnlyDictionary&lt;ExoPathOverrideKey, string&gt;).
    ///
    /// Este e o "ponto de montagem" entre a Fase 1 (ExoPathResolver, que so
    /// aceita overrides ja parseados/validados) e a Fase 2 (ExoToolConfig,
    /// que guarda ExoEntityDefinition cru porque e um ScriptableObject
    /// serializado e precisa tolerar dados legados/nao validados). Usa
    /// ExoCategoryParser/ExoAssetTypeParser (o ponto canonico de conversao
    /// string-para-enum do Core) em vez de reimplementar parsing.
    ///
    /// Puro: so colecoes/string, sem I/O, sem UnityEngine/UnityEditor
    /// (garantido em tempo de compilacao pelo noEngineReferences=true do
    /// asmdef deste assembly). Nao acessa ScriptableObject nem AssetDatabase -
    /// quem chama (ExoToolConfig, fora deste assembly) e responsavel por
    /// extrair a lista de ExoEntityDefinition do asset antes de chamar Build.
    /// </summary>
    public static class ExoOverrideMapBuilder
    {
        /// <summary>
        /// Monta o dicionario de overrides a partir das definicoes. Entradas
        /// invalidas (Categoria ou Tipo que nao batem com nenhum membro do
        /// enum correspondente, ou Pasta nula/vazia) sao ignoradas em
        /// silencio - nunca lancam excecao - e opcionalmente registradas em
        /// "report" como Warning, seguindo a mesma filosofia tolerante de
        /// ExoEntityDefinition (dados legados/nao validados nao devem
        /// derrubar a ferramenta). "entidades" e "report" nulos sao aceitos;
        /// nesse caso o resultado e um dicionario vazio (ou so sem os avisos
        /// de "entidades", se so "report" for nulo).
        ///
        /// Em caso de duas ExoEntityDefinition diferentes definirem override
        /// para a mesma chave exata (Categoria, Nome, Tipo) - o que nao
        /// deveria acontecer com dados bem formados, ja que cada entidade
        /// aparece uma vez na lista - a ultima entrada processada (ordem de
        /// "entidades") vence, igual a semantica normal de atribuicao
        /// repetida em um Dictionary.
        /// </summary>
        public static Dictionary<ExoPathOverrideKey, string> Build(
            IEnumerable<ExoEntityDefinition> entidades,
            ExoBuildReport report = null)
        {
            Dictionary<ExoPathOverrideKey, string> mapa = new Dictionary<ExoPathOverrideKey, string>();
            if (entidades == null)
                return mapa;

            foreach (ExoEntityDefinition definicao in entidades)
            {
                if (definicao == null)
                    continue;

                if (!ExoCategoryParser.TryParse(definicao.Categoria, out ExoCategory categoria))
                {
                    report?.Warning(
                        "Categoria desconhecida \"" + definicao.Categoria + "\" - overrides de pasta da entidade ignorados.",
                        definicao.Nome);
                    continue;
                }

                if (definicao.FolderOverrides == null)
                    continue;

                foreach (ExoFolderOverride overr in definicao.FolderOverrides)
                {
                    if (overr == null)
                        continue;

                    if (!ExoAssetTypeParser.TryParse(overr.Tipo, out ExoAssetType tipo))
                    {
                        report?.Warning(
                            "Tipo de asset desconhecido \"" + overr.Tipo + "\" - override ignorado.",
                            definicao.Nome);
                        continue;
                    }

                    if (string.IsNullOrEmpty(overr.Pasta))
                        continue;

                    ExoPathOverrideKey chave = new ExoPathOverrideKey(categoria, definicao.Nome, tipo);
                    mapa[chave] = overr.Pasta;
                }
            }

            return mapa;
        }
    }
}
