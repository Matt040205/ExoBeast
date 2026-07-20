using System;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Conversao canonica de string para ExoCategory.
    ///
    /// ExoEntityDefinition.Categoria guarda a categoria como string "crua" de
    /// proposito (ver comentario em ExoEntityDefinition.cs): a definicao e um
    /// DTO tolerante a valores legados/nao validados, e quem consome a
    /// definicao decide como e quando validar/converter. Mas sem UM ponto
    /// canonico de conversao dentro do proprio Core, cada chamador (a
    /// comecar pela Fase 2, ExoToolConfig) acabaria escrevendo seu proprio
    /// Enum.Parse/TryParse com casing e tratamento de erro divergentes.
    /// ExoCategoryParser e esse ponto canonico.
    ///
    /// Por que nao usar Enum.TryParse(valor, ignoreCase: true, out categoria)
    /// diretamente: o contrato publico de "ignoreCase" do Enum.TryParse nao
    /// documenta explicitamente independencia de cultura em toda
    /// versao/runtime. Comparar contra ExoCategory.X.ToString() usando
    /// StringComparison.OrdinalIgnoreCase deixa a independencia de cultura
    /// como contrato explicito e auditavel neste arquivo, em vez de um
    /// detalhe de implementacao de terceiros - mesmo raciocinio por tras da
    /// troca de char.ToUpper por char.ToUpperInvariant em
    /// ExoNaming.TowerBaseName.
    ///
    /// Casing: comparacao case-insensitive via
    /// StringComparison.OrdinalIgnoreCase (nunca
    /// CultureInfo.CurrentCulture/ToUpper/ToLower) - "personagens",
    /// "Personagens" e "PERSONAGENS" resolvem todos para
    /// ExoCategory.Personagens, independente de locale/maquina.
    ///
    /// Puro: so string/enum, sem I/O, sem UnityEngine/UnityEditor (garantido
    /// em tempo de compilacao pelo noEngineReferences=true do asmdef deste
    /// assembly).
    ///
    /// Nit da Fase 1 corrigido na Fase 2: a lista de membros usada aqui era
    /// antes um array hardcoded que espelhava o enum a mao (ExoCategory.X,
    /// ExoCategory.Y, ...). Se alguem adicionasse um membro ao enum e
    /// esquecesse de adicionar aqui, TryParse falhava em SILENCIO (retornava
    /// false para um nome de membro que na verdade existe), enquanto
    /// ExoPathResolver.GetCategoryRoot/GetSubfolderName - que usam switch com
    /// "default: throw" - lancam excecao para o mesmo tipo de membro
    /// desconhecido. Dois modos de falha diferentes para a mesma causa raiz
    /// (enum cresceu, algum consumidor nao acompanhou).
    ///
    /// Corrigido derivando a lista via Enum.GetValues(typeof(ExoCategory)) em
    /// vez de mante-la a mao. Isso elimina a classe inteira de bug em vez de
    /// so detecta-la depois: nao ha mais um array separado que possa
    /// divergir do enum, entao nao ha necessidade de um teste de paridade
    /// dedicado (um teste de paridade so pegaria a divergencia se alguem
    /// lembrasse de rodar os testes antes de mergear; derivar do enum torna
    /// a divergencia estruturalmente impossivel). Enum.GetValues nao depende
    /// de CultureInfo - e reflexao pura sobre os membros declarados do tipo,
    /// mesma garantia de independencia de locale que o resto deste arquivo ja
    /// segue. A ordem devolvida por Enum.GetValues nao e problema aqui: este
    /// parser so faz busca linear por nome, a ordem de iteracao e
    /// irrelevante para o resultado.
    /// </summary>
    public static class ExoCategoryParser
    {
        private static readonly ExoCategory[] AllCategories =
            (ExoCategory[])Enum.GetValues(typeof(ExoCategory));

        /// <summary>
        /// Tenta converter <paramref name="valor"/> para ExoCategory. Retorna
        /// false (sem lancar excecao) para null, string vazia, ou qualquer
        /// valor que nao bata (case-insensitive, invariante) com o nome de
        /// nenhum membro de ExoCategory. Em caso de false, <paramref
        /// name="categoria"/> recebe default(ExoCategory).
        /// </summary>
        public static bool TryParse(string valor, out ExoCategory categoria)
        {
            if (!string.IsNullOrEmpty(valor))
            {
                for (int i = 0; i < AllCategories.Length; i++)
                {
                    ExoCategory candidata = AllCategories[i];
                    if (string.Equals(valor, candidata.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        categoria = candidata;
                        return true;
                    }
                }
            }

            categoria = default;
            return false;
        }
    }

    /// <summary>
    /// Conversao canonica de string para ExoAssetType.
    ///
    /// Mesmo raciocinio de ExoCategoryParser (ver comentario na classe
    /// acima, que vive neste mesmo arquivo de proposito):
    /// ExoFolderOverride.Tipo guarda o tipo de asset como string crua para
    /// manter ExoFolderOverride como DTO tolerante, e este e o ponto
    /// canonico para converter essa string para ExoAssetType antes de usar
    /// ExoPathResolver.
    ///
    /// ExoCategoryParser e ExoAssetTypeParser ficam no mesmo arquivo
    /// (ExoEnumParsing.cs) em vez de um arquivo por classe: os dois existem
    /// para resolver exatamente o mesmo problema (conversao canonica de
    /// string para enum do Core, para as duas strings "cruas" de
    /// ExoEntityDefinition/ExoFolderOverride), sao sempre consumidos juntos
    /// ao processar uma unica ExoEntityDefinition (ela tem Categoria E
    /// FolderOverrides[].Tipo), e compartilham a mesma decisao de casing e a
    /// mesma justificativa - manter as duas em um so arquivo evita duplicar
    /// (e arriscar divergir) essa justificativa em dois lugares.
    ///
    /// Casing: mesma regra de ExoCategoryParser -
    /// StringComparison.OrdinalIgnoreCase, nunca cultura da thread.
    ///
    /// Mesmo fix de paridade da Fase 2 aplicado em ExoCategoryParser acima
    /// (ver o comentario la para a justificativa completa): a lista de
    /// membros agora vem de Enum.GetValues(typeof(ExoAssetType)) em vez de um
    /// array hardcoded, eliminando a possibilidade de o array divergir do
    /// enum.
    /// </summary>
    public static class ExoAssetTypeParser
    {
        private static readonly ExoAssetType[] AllAssetTypes =
            (ExoAssetType[])Enum.GetValues(typeof(ExoAssetType));

        /// <summary>
        /// Tenta converter <paramref name="valor"/> para ExoAssetType.
        /// Retorna false (sem lancar excecao) para null, string vazia, ou
        /// qualquer valor que nao bata (case-insensitive, invariante) com o
        /// nome de nenhum membro de ExoAssetType. Em caso de false,
        /// <paramref name="tipo"/> recebe default(ExoAssetType).
        ///
        /// Note que a comparacao e contra o NOME do membro do enum (ex.:
        /// "Animacao", sem acento - ver ExoAssetType em ExoPathResolver.cs),
        /// nao contra o nome de pasta acentuado que ExoPathResolver.
        /// GetSubfolderName gera para exibicao ("Animação"). Essas sao duas
        /// strings diferentes de proposito: uma e a chave estavel do dado
        /// (nome do enum), a outra e a convencao de pasta do projeto.
        /// </summary>
        public static bool TryParse(string valor, out ExoAssetType tipo)
        {
            if (!string.IsNullOrEmpty(valor))
            {
                for (int i = 0; i < AllAssetTypes.Length; i++)
                {
                    ExoAssetType candidato = AllAssetTypes[i];
                    if (string.Equals(valor, candidato.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        tipo = candidato;
                        return true;
                    }
                }
            }

            tipo = default;
            return false;
        }
    }
}
