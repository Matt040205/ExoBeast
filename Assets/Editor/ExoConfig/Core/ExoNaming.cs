using System;
using System.Text.RegularExpressions;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Regras de nomenclatura de assets/prefabs da ferramenta Exo Config,
    /// extraidas de Assets/Editor/ExoPrefabBuilder.cs e
    /// Assets/Editor/ExoPrefabMenu.cs (nao modificados nesta fase).
    ///
    /// Funcao pura: so manipulacao de string/regex, sem I/O, sem
    /// UnityEngine/UnityEditor (garantido em tempo de compilacao pelo
    /// noEngineReferences=true do asmdef deste assembly).
    /// </summary>
    public static class ExoNaming
    {
        private const string TowerPrefix = "Torreta";
        private const string CharacterSuffix = " Variant";
        private const string MaterialSuffix = "_Mat";

        // Mesma regex de ExoPrefabBuilder.FindOriginalPrefab (linha ~495):
        // System.Text.RegularExpressions.Regex.Replace(cleanEntity, @"\d+$", "").
        private static readonly Regex TrailingDigits = new Regex(@"\d+$", RegexOptions.Compiled);

        /// <summary>
        /// Nome do arquivo de modelo (FBX) de destino - so acrescenta a
        /// extensao ".fbx", sem nenhuma outra transformacao. Ex.:
        /// "samurai 3" vira "samurai 3.fbx".
        ///
        /// Unica regra desta classe introduzida na Fase 4 (pipeline): antes
        /// dela, o calculo vivia inline em
        /// ExoPrefabMenu.ExecutarOrganizar (pre-Fase-4) como
        /// "fileName + ".fbx"" direto, sem passar por ExoNaming. Extraido
        /// agora para Assets/Editor/ExoConfig/Pipeline/Steps/ImportAssetsStep.cs
        /// usar o mesmo padrao das demais regras desta classe (todas funcoes
        /// puras e testadas aqui) em vez de concatenar string solta.
        /// </summary>
        public static string ModelFileName(string fbxName)
        {
            RequireFbxName(fbxName);
            return fbxName + ".fbx";
        }

        /// <summary>
        /// Nome do arquivo de textura gerado a partir do nome do FBX - so o
        /// NOME DO ARQUIVO, sem pasta. fbxName aqui e um nome nu (ex.:
        /// "samurai 3"), nunca um caminho completo nem inclui a extensao
        /// ".fbx" (ver os demais metodos desta classe e ExoNamingTests, que
        /// sempre passam nomes assim).
        /// Ex.: "samurai 3" vira "samurai 3T.png".
        ///
        /// O calculo original, ExoPrefabBuilder.BuildMaterial (linha ~451):
        ///   fbxPath.Replace("Modelos", "Texturas").Replace(".fbx", "T.png")
        /// fazia duas coisas de uma vez com dois Replace encadeados sobre um
        /// caminho completo: trocava a PASTA ("Modelos" -> "Texturas") e
        /// trocava o NOME DO ARQUIVO (".fbx" -> "T.png"). Este metodo
        /// implementa so a segunda parte (o nome do arquivo); a primeira
        /// parte (a pasta de destino) agora e responsabilidade de
        /// ExoPathResolver.ResolveFolder / ExoPathResolver.GetSubfolderName
        /// (ExoAssetType.Texturas). Separar "qual pasta" de "qual nome de
        /// arquivo" em duas funcoes puras e testaveis, em vez de um unico
        /// Replace de path, e uma melhoria deliberada sobre o original: o
        /// Replace encadeado era fragil (dependia de "Modelos" aparecer
        /// exatamente uma vez e so onde esperado dentro do caminho inteiro).
        /// </summary>
        public static string TextureFileName(string fbxName)
        {
            RequireFbxName(fbxName);
            return fbxName + "T.png";
        }

        /// <summary>
        /// Nome do arquivo de material gerado a partir do nome do FBX.
        /// Ex.: "samurai 3" vira "samurai 3_Mat.mat".
        /// Espelha ExoPrefabBuilder.BuildMaterial (linha ~437).
        /// </summary>
        public static string MaterialFileName(string fbxName)
        {
            RequireFbxName(fbxName);
            return fbxName + MaterialSuffix + ".mat";
        }

        /// <summary>
        /// Nome do arquivo de prefab de Personagem (Comandante).
        /// Ex.: "samurai 3" vira "samurai 3 Variant.prefab".
        /// Espelha ExoPrefabBuilder.BuildCharacterPrefab (linha ~38).
        /// </summary>
        public static string CharacterPrefabFileName(string fbxName)
        {
            RequireFbxName(fbxName);
            return fbxName + CharacterSuffix + ".prefab";
        }

        /// <summary>
        /// Nome-base (sem extensao) do prefab de Torre correspondente a um
        /// Personagem: "Torreta" + a primeira letra do fbxName em maiusculo + o
        /// restante do fbxName sem nenhuma outra alteracao.
        /// Ex.: "samurai 3" vira "TorretaSamurai 3".
        ///
        /// Espelha ExoPrefabBuilder.BuildCharacterPrefab (linha ~71):
        ///   "Torreta" + char.ToUpper(entityName[0]) + entityName.Substring(1)
        /// com UM desvio deliberado e definitivo do original: aqui usamos
        /// char.ToUpperInvariant em vez de char.ToUpper. O char.ToUpper(char)
        /// original usa a CultureInfo.CurrentCulture da thread - em locale
        /// tr-TR (turco), por exemplo, isso pode produzir uma letra maiuscula
        /// diferente da que sairia em qualquer outra maquina/regiao, ou seja,
        /// o nome do prefab gerado dependeria de configuracao da maquina de
        /// quem roda a ferramenta. Essa e exatamente a classe de bug que esta
        /// refatoracao existe para eliminar (nenhum passo pode depender de
        /// config local). char.ToUpperInvariant e comportamentalmente
        /// identico ao char.ToUpper original para todos os caracteres usados
        /// neste projeto, inclusive acentuados (ex.: 'á' -> 'Á'), entao nenhum
        /// nome de prefab existente muda - so remove a dependencia de locale.
        ///
        /// So a primeira letra vira maiuscula - o resto da string (espacos,
        /// digitos, demais letras) e copiado sem qualquer ToLower/ToUpper
        /// adicional.
        /// </summary>
        public static string TowerBaseName(string fbxName)
        {
            RequireFbxName(fbxName);
            return TowerPrefix + char.ToUpperInvariant(fbxName[0]) + fbxName.Substring(1);
        }

        /// <summary>
        /// Nome do arquivo de prefab de Torre.
        /// Ex.: "samurai 3" vira "TorretaSamurai 3.prefab".
        /// Espelha ExoPrefabBuilder.BuildCharacterPrefab (linha ~72).
        /// </summary>
        public static string TowerPrefabFileName(string fbxName)
        {
            return TowerBaseName(fbxName) + ".prefab";
        }

        /// <summary>
        /// Nome do arquivo de prefab generico (Monstro ou Environment) - sem
        /// sufixo " Variant".
        /// Ex.: "Ponte" vira "Ponte.prefab".
        /// Espelha ExoPrefabBuilder.BuildCharacterPrefab (linha ~40, ramo
        /// "profile == null || profile.entityType != Personagem") e o ramo
        /// "else" de Monstro/Edificio (linha ~126).
        /// </summary>
        public static string GenericPrefabFileName(string fbxName)
        {
            RequireFbxName(fbxName);
            return fbxName + ".prefab";
        }

        /// <summary>
        /// Nome do arquivo do Animator Controller de uma entidade (Personagem
        /// ou Monstro), por convencao. Ex.: "Ayame" vira
        /// "AyameAnimator.controller".
        ///
        /// Fase 7 da refatoracao Exo Config (AnimatorStep). Confirmado contra
        /// o UNICO Animator Controller real que existe no projeto hoje:
        /// Assets/Personagens/Ayame/Animação/AyameAnimator.controller - nome
        /// da entidade ("Ayame", exatamente como cadastrado em
        /// ExoToolConfig.asset) + "Animator" + ".controller", sem nenhuma
        /// outra transformacao (sem CleanEntityName, sem trocar case).
        ///
        /// IMPORTANTE: "nome" aqui e o NOME DA ENTIDADE
        /// (ExoBuildContext.Nome/ExoEntityDefinition.Nome - ex.: "Ayame"), e
        /// DELIBERADAMENTE NAO "fbxName" (ExoBuildContext.FbxFileName - ex.:
        /// "samurai 3") como todos os outros metodos desta classe. Os dois
        /// costumam divergir: o FBX de origem pode ser reimportado com nomes
        /// diferentes ao longo do tempo ("samurai", "samurai 2", "samurai 3"
        /// - confirmado em Assets/Personagens/Ayame/Modelos/), mas o Animator
        /// Controller e AUTORAL, colocado a mao pelo game designer UMA VEZ, e
        /// nomeado a partir do nome ESTAVEL da entidade - exatamente o que a
        /// evidencia real acima confirma ("Ayame", nao "Samurai"/"TorretaSamurai").
        /// Ver AnimatorStep (Assets/Editor/ExoConfig/Pipeline/Steps/AnimatorStep.cs).
        /// </summary>
        public static string AnimatorControllerFileName(string nome)
        {
            RequireNome(nome);
            return nome + "Animator.controller";
        }

        /// <summary>
        /// "Nome limpo" de uma entidade: remove os marcadores "Torreta",
        /// "Variant" e "Completo" (qualquer ocorrencia da substring, case
        /// sensitive), remove digitos finais e remove espacos nas pontas.
        /// Letras acentuadas nunca sao tocadas pela regex de digitos finais.
        ///
        /// Ex.: "samurai 3" vira "samurai"; "TorretaSamurai" vira "Samurai";
        /// "EscorpiaoCompleto" vira "Escorpiao".
        ///
        /// Espelha ExoPrefabBuilder.FindOriginalPrefab (linhas ~494-495):
        ///   entityName.Replace("Torreta", "").Replace("Variant", "").Replace("Completo", "").Trim();
        ///   Regex.Replace(cleanEntity, @"\d+$", "").Trim();
        /// </summary>
        public static string CleanEntityName(string entityName)
        {
            if (entityName == null)
                throw new ArgumentNullException(nameof(entityName));

            string clean = entityName.Replace(TowerPrefix, string.Empty)
                                      .Replace("Variant", string.Empty)
                                      .Replace("Completo", string.Empty)
                                      .Trim();
            clean = TrailingDigits.Replace(clean, string.Empty).Trim();
            return clean;
        }

        private static void RequireFbxName(string fbxName)
        {
            if (string.IsNullOrEmpty(fbxName))
                throw new ArgumentException("[ExoConfig] nome do FBX nao pode ser nulo ou vazio.", nameof(fbxName));
        }

        /// <summary>
        /// Mesmo guard de RequireFbxName, mensagem de erro separada porque o
        /// parametro representa uma coisa DIFERENTE (nome de ENTIDADE, nao de
        /// FBX - ver AnimatorControllerFileName acima). Espelha
        /// ExoPathResolver.RequireNome (mesmo nome, classe diferente - sem
        /// conflito).
        /// </summary>
        private static void RequireNome(string nome)
        {
            if (string.IsNullOrEmpty(nome))
                throw new ArgumentException("[ExoConfig] nome da entidade nao pode ser nulo ou vazio.", nameof(nome));
        }
    }
}
