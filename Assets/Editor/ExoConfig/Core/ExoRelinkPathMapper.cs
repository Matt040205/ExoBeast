using System;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Fase 6 da refatoracao Exo Config: remapeia um caminho relativo (do jeito
    /// que ExoPrefabBuilder.GetRelativePath calcula, ex.: "Samurai/Mesh/Bone")
    /// de uma hierarquia de prefab TEMPLATE original para a hierarquia
    /// recem-construida (possivelmente com o modelo/FBX renomeado), trocando
    /// so o PRIMEIRO segmento do caminho (o nome do no-modelo) e preservando o
    /// resto.
    ///
    /// Substitui o ExoPrefabBuilder.MapRelativePath antigo, que fazia:
    ///   string origToken = "Pivot/" + origFbxName;
    ///   string newToken = "Pivot/" + newFbxName;
    ///   if (origPath.StartsWith(origToken)) return newToken + origPath.Substring(origToken.Length);
    ///   return origPath;
    /// Isso so remapeava caminhos que comecavam literalmente com "Pivot/" - mas
    /// ConfigureAsTower e ConfigureAsEnemy (as UNICAS chamadoras hoje de
    /// CopySerializedValuesAndRelink; Personagem usa
    /// BuildOrUpdateCharacterVariant/ReplaceModelUnderPivot desde a Fase 5 e
    /// NUNCA chama este caminho) nunca criam nenhum "Pivot" - o modelo
    /// instanciado do FBX e sempre filho DIRETO do root (ao lado de
    /// GameObjects vazios/primitivos que o proprio builder cria - ver
    /// ExoPrefabBuilder.FindModelChild). Como origFbxName/newFbxName
    /// ficavam "" (nenhum "Pivot" para achar), o MapRelativePath antigo
    /// devolvia o caminho intocado SEMPRE para Torre/Monstro - funcionava por
    /// acidente quando o nome do modelo nao mudava entre execucoes (o
    /// caminho relativo original ja batia com o novo por coincidencia de
    /// nome) e quebrava em silencio (Find falha, referencia gravada como
    /// null) assim que o FBX era reimportado com um nome diferente (ex.: Unity
    /// sufixando "Samurai" -> "Samurai 2" ao nao sobrescrever um arquivo
    /// existente - cenario real, ver Assets/Entidades/Inimigos/Aranhaaa.fbx e
    /// Aguiaa.fbx).
    ///
    /// Esta versao nao assume NENHUM nome de pasta fixo: o chamador
    /// (ExoPrefabBuilder.FindModelChild) descobre "qual e o no-modelo" pela
    /// IDENTIDADE ESTRUTURAL do objeto (e uma instancia de Model Prefab),
    /// nao por convencao de nome/pasta - e passa o NOME desse no diretamente
    /// como origModelName/newModelName, sem prefixo. Isso funciona
    /// identicamente para Torre (modelo filho direto do root, ao lado de
    /// "GameObject"/"CirculoSeletor") e Monstro (modelo filho direto do
    /// root, ao lado de "DamagePopupPosition"/"Sphere"/"Indicador_Aggro"/
    /// "Dissolvevfx") sem precisar de dois caminhos de codigo diferentes.
    ///
    /// Puro: so manipulacao de string, sem I/O, sem UnityEngine/UnityEditor
    /// (garantido em tempo de compilacao pelo noEngineReferences=true deste
    /// assembly).
    /// </summary>
    public static class ExoRelinkPathMapper
    {
        /// <summary>
        /// origPath: caminho relativo original (ex.: "Samurai", "Samurai/Mesh",
        /// "Samurai/Mesh/Bone01"). origModelName/newModelName: nome do no-modelo
        /// (filho direto do root) nas hierarquias origem/destino - SEM prefixo
        /// de pasta.
        ///
        /// Se origModelName ou newModelName forem nulos/vazios (nenhum no-modelo
        /// foi encontrado em uma das duas hierarquias), devolve origPath
        /// intocado - mesma postura defensiva do metodo original: sem dado
        /// suficiente para remapear, nao inventa um caminho, so desiste do
        /// remapeamento (o chamador trata "nao achou o Transform mapeado" como
        /// "nao relinka essa propriedade", nunca lanca excecao).
        ///
        /// Dois casos:
        /// 1. origPath == origModelName (referencia aponta para o proprio
        ///    no-modelo, ex.: um campo que referencia o GameObject do FBX
        ///    inteiro) -> devolve newModelName diretamente.
        /// 2. origPath comeca com "origModelName/" (referencia aponta para
        ///    ALGO DENTRO do modelo, ex.: um osso) -> troca so esse primeiro
        ///    segmento, preserva o resto do caminho igual.
        /// Nos dois casos a comparacao de prefixo usa StringComparison.Ordinal
        /// (nunca comparacao sensivel a cultura) - mesma motivacao ja aplicada
        /// em ExoNaming.TowerBaseName (Fase 1): o resultado nao pode depender
        /// de CultureInfo.CurrentCulture da maquina que roda a ferramenta.
        /// </summary>
        public static string MapRelativePath(string origPath, string origModelName, string newModelName)
        {
            if (origPath == null) return null;
            if (string.IsNullOrEmpty(origModelName) || string.IsNullOrEmpty(newModelName))
                return origPath;

            if (string.Equals(origPath, origModelName, StringComparison.Ordinal))
                return newModelName;

            string origToken = origModelName + "/";
            if (origPath.StartsWith(origToken, StringComparison.Ordinal))
            {
                string newToken = newModelName + "/";
                return newToken + origPath.Substring(origToken.Length);
            }

            return origPath;
        }
    }
}
