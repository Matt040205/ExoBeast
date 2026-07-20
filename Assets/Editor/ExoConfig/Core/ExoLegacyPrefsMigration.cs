using System;
using System.Collections.Generic;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Interpreta um snapshot cru de chaves/valores no formato legado do
    /// EditorPrefs da Exo Config (ver ExoConfigWindow/ExoPrefabMenu
    /// pre-Fase-2) e produz uma lista de ExoEntityDefinition.
    ///
    /// Formato legado (documentado tambem em ExoPathResolver.cs e
    /// ExoCategory.cs):
    ///   chave de lista       = nome da categoria (ex.: "Personagens")
    ///                           -> CSV de nomes de entidade
    ///   chave de override     = "{Categoria}_{Nome}_{Sufixo}"
    ///                           -> caminho de pasta cru
    ///     Sufixo: "Mat" (Materiais), "Mod" (Modelos), "Tex" (Texturas),
    ///             "Pre" (Prefabs), "Ani" (Animacao)
    ///
    /// Deliberadamente NAO le EditorPrefs diretamente - recebe um delegate
    /// "rawGet" (string chave -> string valor cru, ou null/vazio se a chave
    /// nao existe) injetado pelo chamador. Isso e o mesmo padrao de
    /// ExoPathResolver.ResolveFolder (overrides injetados, nunca lidos
    /// diretamente): mantem esta classe pura e testavel sem depender do
    /// EditorPrefs real do Windows (que so existe dentro do Editor da Unity),
    /// e sem UnityEngine/UnityEditor (garantido em tempo de compilacao pelo
    /// noEngineReferences=true do asmdef deste assembly). Quem efetivamente
    /// chama EditorPrefs.GetString fica na camada de fora deste assembly
    /// (Assembly-CSharp-Editor), que so embrulha "rawGet" em cima do
    /// EditorPrefs real.
    ///
    /// Vinda vazia (rawGet sempre retornando null/"") degrada sem erro,
    /// devolvendo uma lista vazia - e exatamente o caso desta maquina, onde o
    /// EditorPrefs do dev anterior nao existe. Esta classe existe para
    /// quando "rawGet" vier de uma maquina com dados reais.
    /// </summary>
    public static class ExoLegacyPrefsMigrator
    {
        /// <summary>
        /// Mapa sufixo-legado -> ExoAssetType. Fica privado e local a esta
        /// classe de proposito: e conhecimento do formato ANTIGO de chave do
        /// EditorPrefs, nao uma convencao do dominio (ExoPathResolver/
        /// ExoNaming nunca devem saber desses sufixos - ver comentario em
        /// ExoPathResolver.cs sobre ExoPathOverrideKey ser o "equivalente
        /// puro" do prefixo de chave do EditorPrefs).
        /// </summary>
        private static readonly (string Sufixo, ExoAssetType Tipo)[] PathSuffixes =
        {
            ("Mat", ExoAssetType.Materiais),
            ("Mod", ExoAssetType.Modelos),
            ("Tex", ExoAssetType.Texturas),
            ("Pre", ExoAssetType.Prefabs),
            ("Ani", ExoAssetType.Animacao),
        };

        /// <summary>
        /// Le, via "rawGet", a chave de lista de cada ExoCategory e depois as
        /// chaves de override de pasta de cada entidade encontrada. Devolve
        /// uma ExoEntityDefinition por entidade (Nome + Categoria sempre
        /// preenchidos; FolderOverrides so contem os sufixos com valor nao
        /// vazio em "rawGet", e nunca inclui Animacao para uma categoria que
        /// ExoPathResolver.SupportsAssetType nao suporta - hoje so
        /// Environment).
        ///
        /// "rawGet" nulo, ou que retorna null/"" para toda chave, devolve
        /// lista vazia sem lancar excecao (degradacao exigida para a maquina
        /// onde nao ha EditorPrefs legado).
        /// </summary>
        public static List<ExoEntityDefinition> ParseEntities(Func<string, string> rawGet, ExoBuildReport report = null)
        {
            List<ExoEntityDefinition> resultado = new List<ExoEntityDefinition>();
            if (rawGet == null)
                return resultado;

            foreach (ExoCategory categoria in (ExoCategory[])Enum.GetValues(typeof(ExoCategory)))
            {
                string listaCrua = rawGet(categoria.ToString());
                if (string.IsNullOrEmpty(listaCrua))
                    continue;

                string[] nomes = listaCrua.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string nome in nomes)
                {
                    ExoEntityDefinition definicao = new ExoEntityDefinition
                    {
                        Nome = nome,
                        Categoria = categoria.ToString()
                    };

                    string prefixo = categoria + "_" + nome + "_";
                    foreach ((string sufixo, ExoAssetType tipo) in PathSuffixes)
                    {
                        if (!ExoPathResolver.SupportsAssetType(categoria, tipo))
                            continue;

                        string valor = rawGet(prefixo + sufixo);
                        if (!string.IsNullOrEmpty(valor))
                            definicao.FolderOverrides.Add(new ExoFolderOverride(tipo.ToString(), valor));
                    }

                    resultado.Add(definicao);
                    report?.Info("Entidade legada encontrada no EditorPrefs: " + nome, categoria.ToString());
                }
            }

            return resultado;
        }
    }
}
