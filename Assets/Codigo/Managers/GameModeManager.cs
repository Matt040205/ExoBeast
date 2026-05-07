using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using ExoBeasts.Multiplayer.Core;

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
        public static bool HasInstance => Instance != null;
        public static GameModeManager TryGetExistingInstance() => Instance;

        [SerializeField] private string escolherPersonagemScene = "EscolherPersonagem";
        [SerializeField] private string lobbyScene = "LobbyScene";

        private Coroutine _sceneTransitionRoutine;

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

        public static GameModeManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("GameModeManager");
            return go.AddComponent<GameModeManager>();
        }

        /// <summary>
        /// Chamado pelo botao "Jogar Solo" no MenuManager.
        /// Seta modo singleplayer e vai para selecao de personagem.
        /// O NetworkManager.StartHost() sera chamado no GameSetupManager
        /// ao carregar a cena de jogo.
        /// </summary>
        public void StartSingleplayer()
        {
            QueueSceneTransition(GameMode.Singleplayer, escolherPersonagemScene);
        }

        /// <summary>
        /// Chamado pelo botao "Jogar Online" no MenuManager.
        /// Seta modo multiplayer e vai para lobby (auth + matchmaking).
        /// </summary>
        public void StartMultiplayer()
        {
            QueueSceneTransition(GameMode.Multiplayer, lobbyScene);
        }

        public static void ReturnToSingleplayer()
        {
            CurrentMode = GameMode.Singleplayer;
        }

        public static void ReturnToMultiplayerLobby()
        {
            CurrentMode = GameMode.Multiplayer;
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

        private void QueueSceneTransition(GameMode targetMode, string sceneName)
        {
            if (_sceneTransitionRoutine != null)
            {
                Debug.LogWarning($"[GameModeManager] Transicao para '{sceneName}' ignorada porque outra transicao ja esta em andamento.");
                return;
            }

            _sceneTransitionRoutine = StartCoroutine(SceneTransitionRoutine(targetMode, sceneName));
        }

        private IEnumerator SceneTransitionRoutine(GameMode targetMode, string sceneName)
        {
            yield return MultiplayerRuntimeReset.ResetToOfflineLocal();

            CurrentMode = targetMode;
            _sceneTransitionRoutine = null;
            SceneManager.LoadScene(sceneName);
        }
    }
}
