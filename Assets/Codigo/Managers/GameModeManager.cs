using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace ExoBeasts.Managers
{
    public enum GameMode { Singleplayer, Multiplayer }

    /// <summary>
    /// ── GameModeManager ────────────────────────────────────
    /// Singleton persistente que gerencia o modo de jogo atual.
    ///
    ///  ▸ StartSingleplayer(): inicia como Host local sem lobby
    ///  ▸ StartMultiplayer(): redireciona para fluxo EOS Auth + Lobby
    ///  ▸ LoadSceneSafe(): carrega cena via NGO em sessao de rede ou SceneManager fora dela
    ///  ▸ IsNetworkSession: verifica se NetworkManager esta ativo
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }
        public static GameMode CurrentMode { get; private set; } = GameMode.Singleplayer;

        [SerializeField] private string escolherPersonagemScene = "EscolherPersonagem";
        [SerializeField] private string lobbyScene = "LobbyScene";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Chamado pelo botao "Jogar Solo" no MenuManager.
        /// Seta modo singleplayer e vai para selecao de personagem.
        /// O NetworkManager.StartHost() sera chamado no GameSetupManager
        /// ao carregar a cena de jogo.
        /// </summary>
        public void StartSingleplayer()
        {
            CurrentMode = GameMode.Singleplayer;
            SceneManager.LoadScene(escolherPersonagemScene);
        }

        /// <summary>
        /// Chamado pelo botao "Jogar Online" no MenuManager.
        /// Seta modo multiplayer e vai para lobby (auth + matchmaking).
        /// </summary>
        public void StartMultiplayer()
        {
            CurrentMode = GameMode.Multiplayer;
            SceneManager.LoadScene(lobbyScene);
        }

        public static void ReturnToSingleplayer()
        {
            CurrentMode = GameMode.Singleplayer;
        }

        /// <summary>
        /// Auxiliar: verifica se estamos em modo multiplayer com clientes remotos.
        /// Util para decidir se usa NetworkManager.SceneManager ou SceneManager padrao.
        /// </summary>
        public static bool IsNetworkSession
        {
            get
            {
                return NetworkManager.Singleton != null &&
                       NetworkManager.Singleton.IsListening;
            }
        }

        /// <summary>
        /// Carrega cena de forma segura (usa NGO se em sessao de rede).
        /// Apenas o servidor/host pode chamar em sessao de rede.
        /// </summary>
        public static void LoadSceneSafe(string sceneName)
        {
            if (IsNetworkSession && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(
                    sceneName, LoadSceneMode.Single);
            }
            else if (!IsNetworkSession)
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
