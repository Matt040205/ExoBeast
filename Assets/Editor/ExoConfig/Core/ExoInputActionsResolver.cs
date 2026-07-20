using System;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Resolve qual dos dois caminhos conhecidos do InputActionAsset do
    /// projeto existe de fato, tentando o acentuado primeiro - MESMA ORDEM
    /// que ExoPrefabBuilder.ConfigureAsCharacter usava (INPUT_ACTIONS_PATH_ALT
    /// antes de INPUT_ACTIONS_PATH). Historico: ConfigureAsCharacter foi
    /// REMOVIDO na Fase 5 (substituido por
    /// ExoPrefabBuilder.BuildOrUpdateCharacterVariant - o Personagem agora
    /// herda PlayerInput.actions de profile.basePrefab via Prefab Variant,
    /// nunca mais resolvido/atribuido por codigo) - as duas constantes
    /// INPUT_ACTIONS_PATH/INPUT_ACTIONS_PATH_ALT tambem foram removidas de
    /// ExoPrefabBuilder.cs junto. BuildPrefabStep.cs (Fase 4) chamava este
    /// resolver so para REPORTAR (Info/Warning) se aqueles caminhos
    /// resolveriam antes de ConfigureAsCharacter rodar de fato; a Fase 5
    /// removeu essa chamada tambem (continuar reportando sobre um mecanismo
    /// que nao existe mais seria informacao incorreta no relatorio).
    ///
    /// Confirmado no disco (Fase 4, via "find Assets -iname Configura*"): SO
    /// o caminho acentuado
    /// ("Assets/Configurações/Settings/InputSystem_Actions.inputactions")
    /// existe neste projeto hoje; o sem acento
    /// ("Assets/Configuracoes/Settings/InputSystem_Actions.inputactions") e
    /// so um fallback que nunca resolve a nada nesta maquina.
    ///
    /// Esta classe (e seus testes, ExoInputActionsResolverTests) foi
    /// deliberadamente MANTIDA na Fase 5 mesmo sem nenhum chamador de
    /// producao remanescente: apagar um modulo Core testado nao estava no
    /// escopo explicito da fase, e a logica pura continua correta e
    /// reutilizavel (ex.: por uma futura Fase de validacao). Ver o briefing
    /// da Fase 5 e Assets/Diretrizes_Multiagente.md.
    ///
    /// Puro: recebe "existsAt" como delegate (mesmo padrao de
    /// ExoLegacyPrefsMigrator.ParseEntities/rawGet, no mesmo arquivo deste
    /// assembly) em vez de chamar AssetDatabase diretamente - sem isso
    /// violaria noEngineReferences=true deste assembly (garantido em tempo
    /// de compilacao), e fica testavel sem depender do Editor.
    /// </summary>
    public static class ExoInputActionsResolver
    {
        public const string AccentedPath = "Assets/Configurações/Settings/InputSystem_Actions.inputactions";
        public const string AsciiPath = "Assets/Configuracoes/Settings/InputSystem_Actions.inputactions";

        /// <summary>
        /// Tenta AccentedPath primeiro, depois AsciiPath. Devolve o primeiro
        /// caminho para o qual "existsAt" retornar true, ou null se nenhum
        /// dos dois existir. "existsAt" nulo lanca ArgumentNullException (nao
        /// ha um "default" razoavel para "existe ou nao" sem o predicado).
        /// </summary>
        public static string Resolve(Func<string, bool> existsAt)
        {
            if (existsAt == null)
                throw new ArgumentNullException(nameof(existsAt));

            if (existsAt(AccentedPath)) return AccentedPath;
            if (existsAt(AsciiPath)) return AsciiPath;
            return null;
        }
    }
}
