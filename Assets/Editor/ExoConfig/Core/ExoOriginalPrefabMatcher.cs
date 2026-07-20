using System;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Resultado da comparacao entre um prefab candidato (achado por
    /// AssetDatabase.FindAssets) e o nome de entidade buscado, dentro de
    /// ExoPrefabBuilder.FindOriginalPrefab.
    /// </summary>
    public enum ExoOriginalPrefabMatchKind
    {
        /// <summary>Nao bate de nenhuma forma (nem exato, nem aproximado).</summary>
        None,

        /// <summary>
        /// Nome limpo do candidato (ExoNaming.CleanEntityName) e IGUAL ao nome
        /// limpo buscado - correspondencia inequivoca, nunca ambigua entre
        /// candidatos distintos (nomes de arquivo sao unicos numa pasta).
        /// </summary>
        Exact,

        /// <summary>
        /// Nome limpo do candidato CONTEM o nome limpo buscado como substring,
        /// mas nao e igual - correspondencia AMBIGUA por construcao (pode achar
        /// mais de um candidato; qual "ganha" depende da ordem de
        /// AssetDatabase.FindAssets, que nao e garantida). Nunca deve ser
        /// tratada como certeza pelo chamador.
        /// </summary>
        Fuzzy
    }

    /// <summary>
    /// Fase 6 da refatoracao Exo Config: extrai e corrige a logica de
    /// comparacao de ExoPrefabBuilder.FindOriginalPrefab, que antes desta fase
    /// fazia so:
    ///   if (name.ToLower().Contains(cleanEntity.ToLower())) { ... primeiro que achar, usa ... }
    ///
    /// Isso e um risco JA CONFIRMADO no disco deste projeto, nao hipotetico:
    /// Assets/Entidades/Inimigos/ tem tanto "Aguia.prefab" quanto
    /// "Aguiaa.prefab", tanto "Aranha.prefab" quanto "Aranhaa.prefab" (e ate
    /// "Aranhaaa.fbx"/"Aguiaa.fbx" do lado dos modelos-fonte, confirmado nesta
    /// fase) - com Contains puro, buscar "Aguia" bate nos DOIS
    /// ("Aguiaa".Contains("Aguia") tambem e true) e a ordem de
    /// AssetDatabase.FindAssets nao e deterministica: qual prefab "ganha" pode
    /// mudar entre execucoes, sem nenhum aviso.
    ///
    /// Fix: priorizar IGUALDADE EXATA (apos limpar os dois lados com a MESMA
    /// funcao, ExoNaming.CleanEntityName - ja existe e ja e testada desde a
    /// Fase 1, nao duplicada aqui). Fuzzy (Contains) continua existindo como
    /// FALLBACK - so quando nenhum candidato bate exato - porque e o mecanismo
    /// que resolve o cenario real que esta fase precisa consertar (FBX
    /// reimportado como "Samurai 2": nome limpo "Samurai" bate EXATO contra o
    /// candidato "TorretaSamurai" limpo, que tambem vira "Samurai" - nao
    /// precisa de fuzzy nesse caso). O chamador (ExoPrefabBuilder.FindOriginalPrefab)
    /// e responsavel por nunca tratar um resultado Fuzzy como silencioso -
    /// sempre avisa (Debug.LogWarning/report.Warning) quando usa um match
    /// aproximado.
    ///
    /// Por que limpar os DOIS lados (candidato E busca) com CleanEntityName,
    /// em vez de so a busca (como o codigo original fazia): candidatos de
    /// Torre tem o prefixo "Torreta" no NOME DE ARQUIVO real (ex.:
    /// "TorretaSamurai.prefab"), que CleanEntityName remove - sem limpar o
    /// candidato tambem, "TorretaSamurai" nunca seria igual a "Samurai" (a
    /// busca, ja limpa), so um Contains funcionaria. Limpando os dois lados,
    /// "TorretaSamurai" limpo ("Samurai") bate EXATO contra "TorretaSamurai 2"
    /// limpo ("Samurai") - elimina a ambiguidade tambem para Torre, nao so
    /// para Monstro.
    ///
    /// Puro: sem I/O, sem UnityEngine/UnityEditor (noEngineReferences=true
    /// deste assembly). Nao decide qual pasta olhar nem le AssetDatabase - so
    /// classifica UM par (candidato, busca) por vez; o loop sobre os
    /// candidatos de uma pasta continua em ExoPrefabBuilder.FindOriginalPrefab
    /// (que tambem preserva, sem mudanca, o filtro Torre/nao-Torre por prefixo
    /// de nome cru - isso e convencao de nome compartilhada entre categorias,
    /// nao um dado de asset que precise de limpeza).
    /// </summary>
    public static class ExoOriginalPrefabMatcher
    {
        public static ExoOriginalPrefabMatchKind Classify(string candidateFileName, string searchEntityName)
        {
            string candidateClean = ExoNaming.CleanEntityName(candidateFileName ?? string.Empty);
            string searchClean = ExoNaming.CleanEntityName(searchEntityName ?? string.Empty);

            if (candidateClean.Length == 0 || searchClean.Length == 0)
                return ExoOriginalPrefabMatchKind.None;

            if (string.Equals(candidateClean, searchClean, StringComparison.OrdinalIgnoreCase))
                return ExoOriginalPrefabMatchKind.Exact;

            if (candidateClean.ToLowerInvariant().Contains(searchClean.ToLowerInvariant()))
                return ExoOriginalPrefabMatchKind.Fuzzy;

            return ExoOriginalPrefabMatchKind.None;
        }
    }
}
