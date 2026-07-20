using System;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Fase 5, item 5 do escopo: transforma em codigo a regra durável
    /// documentada na memoria do projeto ("Prefab Variants - fileID quebrado
    /// em builds standalone", bug original 29 Abril 2026, fix 2 Maio 2026):
    /// quando um ScriptableObject referencia um fileID "dentro" de um Prefab
    /// Variant, esse fileID precisa aparecer LITERALMENTE no YAML do arquivo
    /// .prefab do variant - senao a referencia resolve no Editor via
    /// AssetDatabase (que tolera o fileID "virtual", herdado do prefab base
    /// mas nunca serializado no variant) mas vira null numa build standalone
    /// (que faz busca estrita pelo fileID dentro do proprio asset
    /// serializado).
    ///
    /// Por que agora: a Fase 5 introduz o uso de verdade de Prefab Variants
    /// para Personagem (ExoPrefabBuilder.BuildOrUpdateCharacterVariant) -
    /// exatamente a categoria de asset onde esse bug historico ja mordeu o
    /// projeto uma vez (na epoca, com Monstros). Este verificador existe e
    /// esta testado a partir de agora porque e agora que o risco volta a
    /// existir de verdade; ele ainda NAO esta ligado a nenhum step do
    /// pipeline (isso e trabalho de uma fase futura - ver ExoBuildPipeline/
    /// IExoBuildStep - por exemplo um "ValidateStep").
    ///
    /// Puro (sem I/O, sem UnityEngine/UnityEditor - garantido em tempo de
    /// compilacao pelo noEngineReferences=true deste assembly): recebe o
    /// TEXTO ja lido de um .prefab, nao um caminho de arquivo. Ler o arquivo
    /// (File.ReadAllText, AssetDatabase, etc.) e responsabilidade de quem
    /// chama, fora deste assembly.
    /// </summary>
    public static class ExoFileIdPresenceChecker
    {
        /// <summary>
        /// Devolve true se "fileId" aparecer literalmente no YAML de um
        /// prefab - como REFERENCIA ("fileID: 123", a forma que
        /// "{fileID: 123, guid: ..., type: ...}" usa) OU como a ANCORA de
        /// definicao do proprio objeto ("&123", a forma que
        /// "--- !u!114 &123" usa). Qualquer uma das duas formas conta como
        /// "existe literalmente no YAML" - e exatamente a pergunta que a
        /// regra duravel do projeto pede para validar antes de confiar numa
        /// referencia de ScriptableObject para dentro de um Prefab Variant.
        ///
        /// Faz correspondencia de NUMERO INTEIRO, nunca de prefixo: "123" nao
        /// "acha" "fileID: 1234" nem "&1234" (olha o caractere seguinte ao
        /// numero - se for outro digito, nao e o mesmo fileID, e a busca
        /// continua a partir dali). fileIDs negativos (comuns em componentes
        /// Unity - ex.: o NetworkObject de Assets/Personagens/Player 1.prefab
        /// usa "&-8535913011432277912", confirmado nesta fase) funcionam sem
        /// tratamento especial: o "-" e apenas parte do token de busca.
        ///
        /// yamlText ou fileId nulos ou vazios sempre devolvem false, nunca
        /// lancam excecao: um YAML vazio genuinamente nao contem nenhum
        /// fileID, e um fileId vazio/nulo nao e uma pergunta que faca sentido
        /// responder "sim".
        /// </summary>
        public static bool ContainsFileId(string yamlText, string fileId)
        {
            if (string.IsNullOrEmpty(yamlText) || string.IsNullOrEmpty(fileId))
                return false;

            return HasNumericToken(yamlText, "fileID: " + fileId)
                || HasNumericToken(yamlText, "&" + fileId);
        }

        /// <summary>
        /// Procura todas as ocorrencias de "token" em "haystack" e devolve
        /// true na primeira em que o caractere IMEDIATAMENTE SEGUINTE ao
        /// token nao for um digito (ou o token terminar a string) - ou seja,
        /// o numero em "token" nao continua com mais digitos alem do que foi
        /// pedido. Ocorrencias onde o numero continua (ex.: token "fileID: 123"
        /// dentro de "fileID: 1234") sao descartadas e a busca continua a
        /// partir do proximo caractere.
        /// </summary>
        private static bool HasNumericToken(string haystack, string token)
        {
            int searchFrom = 0;
            while (true)
            {
                int index = haystack.IndexOf(token, searchFrom, StringComparison.Ordinal);
                if (index < 0) return false;

                int afterIndex = index + token.Length;
                bool boundaryOk = afterIndex >= haystack.Length || !char.IsDigit(haystack[afterIndex]);
                if (boundaryOk) return true;

                searchFrom = index + 1;
            }
        }
    }
}
