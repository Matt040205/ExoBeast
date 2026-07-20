using System.Text.RegularExpressions;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Fase 7 da refatoracao Exo Config (ValidateStep): extrai o fileID que um
    /// campo de referencia de Object (ex.: "commanderPrefab", "towerPrefab" em
    /// CharacterBase; "enemyPrefab" em EnemyDataSO) guarda, a partir do TEXTO
    /// YAML JA LIDO do proprio arquivo .asset do ScriptableObject - mesmo
    /// padrao de ExoFileIdPresenceChecker (recebe texto, nao caminho; ler o
    /// arquivo e responsabilidade de quem chama, fora deste assembly).
    ///
    /// Por que ler o TEXTO SERIALIZADO em vez de, por exemplo,
    /// UnityEditor.Unsupported.GetLocalIdentifierInFile sobre o GameObject
    /// live referenciado pelo campo (a alternativa (b) cogitada no briefing
    /// desta fase): a pergunta que ValidateStep precisa responder e "o que
    /// esta LITERALMENTE gravado em disco bate com o que esta LITERALMENTE
    /// gravado no prefab?" - exatamente a mesma pergunta que
    /// ExoFileIdPresenceChecker.ContainsFileId ja responde do lado do prefab
    /// (le o YAML salvo, nao pergunta ao Editor "quem e correspondente a
    /// quem" via AssetDatabase). GetLocalIdentifierInFile, ao contrario,
    /// consulta o MESMO modelo de objeto live/tolerante do Editor que
    /// resolve fileIDs "virtuais" de Prefab Variants sem reclamar - e
    /// exatamente esse comportamento tolerante do Editor que causa o bug
    /// historico do projeto (resolve no Editor, vira null em build
    /// standalone). Usar GetLocalIdentifierInFile arriscaria reconstruir o
    /// MESMO fileID "virtual" que o Editor tolera, mascarando de volta o
    /// problema que este step existe para pegar. Ler o byte serializado (via
    /// File.ReadAllText, fora deste assembly) e comparar texto contra texto
    /// e a unica forma de simular fielmente o que uma build standalone
    /// realmente ve. Beneficio secundario (nao a razao principal): tambem
    /// fica puro/testavel no Core, como o resto desta classe de decisao no
    /// projeto (ExoFileIdPresenceChecker, ExoRelinkPathMapper, etc.).
    ///
    /// Puro: so string/regex, sem I/O, sem UnityEngine/UnityEditor (garantido
    /// em tempo de compilacao pelo noEngineReferences=true deste assembly).
    /// </summary>
    public static class ExoScriptableObjectReferenceParser
    {
        /// <summary>
        /// Extrai o fileID que o campo "fieldName" guarda dentro de
        /// "yamlText" - o texto YAML de um unico documento serializado (ex.:
        /// um .asset de CharacterBase/EnemyDataSO), no formato real que a
        /// Unity gera para um campo de referencia de Object:
        ///   fieldName: {fileID: 1234567890, guid: ..., type: 3}
        /// (confirmado contra Assets/Personagens/Ayame/DataScripableObjects/Ayame.asset
        /// - "commanderPrefab"/"towerPrefab" - e Assets/CoreScripts/Enemy/Aranha.asset
        /// - "enemyPrefab" - nesta fase).
        ///
        /// Devolve o fileID como string (para alimentar direto
        /// ExoFileIdPresenceChecker.ContainsFileId, que tambem trabalha com
        /// string) ou null se: yamlText/fieldName forem nulos/vazios; o campo
        /// nao for encontrado; ou o campo existir mas apontar para "nenhuma
        /// referencia" (fileID 0 - a forma padrao da Unity para uma
        /// referencia de Object nao atribuida). Em nenhum caso lanca excecao
        /// - "nao ha nada para validar" e uma resposta valida, nao um erro de
        /// parsing.
        ///
        /// So considera a PRIMEIRA ocorrencia de uma linha "fieldName:
        /// {fileID: ...}" (comeco de linha, so espacos/tabs antes do nome do
        /// campo - nunca acha "fieldName" como sufixo de outro campo, ex.:
        /// buscar "Prefab" nao "acha" dentro de "commanderPrefab: {...}").
        /// Nomes de campo serializados pela Unity sao unicos por documento
        /// MonoBehaviour, entao uma unica ocorrencia e sempre o suficiente.
        /// </summary>
        public static string ExtractFileId(string yamlText, string fieldName)
        {
            if (string.IsNullOrEmpty(yamlText) || string.IsNullOrEmpty(fieldName))
                return null;

            Regex pattern = new Regex(
                @"(?:^|\n)[ \t]*" + Regex.Escape(fieldName) + @":[ \t]*\{fileID:[ \t]*(-?\d+)",
                RegexOptions.None);

            Match match = pattern.Match(yamlText);
            if (!match.Success)
                return null;

            string fileId = match.Groups[1].Value;
            return fileId == "0" ? null : fileId;
        }

        /// <summary>
        /// Extrai o guid que a MESMA referencia de "fieldName" guarda (o
        /// campo "guid" dentro do mesmo "{fileID: ..., guid: ..., type: ...}"
        /// - ver ExtractFileId para o formato completo). Existe porque
        /// ExoFileIdPresenceChecker.ContainsFileId (Fase 5) verifica SO o
        /// numero do fileID, nunca o guid - e fileIDs "bem conhecidos" que a
        /// Unity atribui por CONVENCAO ao objeto principal de um modelo
        /// importado (ex.: a raiz de um FBX) se repetem entre arquivos
        /// DIFERENTES com frequencia real, nao hipotetica: confirmado nesta
        /// fase que o fileID 919132149155446097 aparece como raiz de modelo
        /// em pelo menos dois GUIDs de FBX distintos deste projeto (visto em
        /// dados de teste reais da Fase 6 E reproduzido ao vivo nesta fase).
        /// Sem checar o guid, ValidateStep podia reportar "confirmado" para
        /// uma referencia que aponta para um asset COMPLETAMENTE DIFERENTE,
        /// so porque os dois compartilham por coincidencia o mesmo fileID
        /// local - um falso positivo pior que nao validar nada. ValidateStep
        /// usa este metodo para confirmar PRIMEIRO que o guid da referencia
        /// bate com o guid do prefab que esta sendo validado, antes de sequer
        /// perguntar se o fileID aparece no YAML dele.
        ///
        /// Devolve null nos mesmos casos de ExtractFileId (entrada
        /// nula/vazia, campo nao encontrado) - nunca lanca excecao.
        /// </summary>
        public static string ExtractGuid(string yamlText, string fieldName)
        {
            if (string.IsNullOrEmpty(yamlText) || string.IsNullOrEmpty(fieldName))
                return null;

            Regex pattern = new Regex(
                @"(?:^|\n)[ \t]*" + Regex.Escape(fieldName) + @":[ \t]*\{fileID:[ \t]*-?\d+,[ \t]*guid:[ \t]*([0-9a-fA-F]+)",
                RegexOptions.None);

            Match match = pattern.Match(yamlText);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
