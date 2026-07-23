using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Text;
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
                LoadLocalSceneMppmSafe(sceneName);
            }
        }

        // BUG FIX (2026-05-21): Unity 6 + MPPM v1.6.3 tem bug onde clones falham ao resolver
        // cenas via SceneManager.LoadScene(name) usando shared scene list, mesmo com a cena
        // listada em EditorBuildSettings. Workaround: resolver build index via SceneUtility
        // e usar LoadScene(index). Mantemos fallback por path completo para evitar ambiguidade
        // quando ha listas de cenas divergentes no Build Profiles/MPPM.
        // Sintoma sem este fix: clones MPPM travam no MenuScene ao clicar "Multiplayer" com
        // erro "Scene 'X' couldn't be loaded because it has not been added to the active build
        // profile or shared scene list".
        private static void LoadLocalSceneMppmSafe(string sceneName)
        {
            string scenePath = GetScenePath(sceneName);
            int buildIndex = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (buildIndex >= 0)
            {
                SceneManager.LoadScene(buildIndex);
                return;
            }

#if UNITY_EDITOR
            if (TryLoadSceneInEditorPlayMode(scenePath))
            {
                return;
            }
#endif

            Debug.LogWarning(
                $"[GameModeManager] Cena '{sceneName}' nao resolveu via build index para path '{scenePath}'.\n" +
                "Build scenes visiveis para este processo:\n" +
                GetBuildSceneListForLog() +
                "\nTentando carregar por path completo (fallback).");
            SceneManager.LoadScene(scenePath);
        }

#if UNITY_EDITOR
        private static bool TryLoadSceneInEditorPlayMode(string scenePath)
        {
            if (!Application.isEditor || !Application.isPlaying)
            {
                return false;
            }

            try
            {
                Debug.LogWarning(
                    $"[GameModeManager] Cena '{scenePath}' nao esta disponivel na lista de build deste processo.\n" +
                    "Carregando via EditorSceneManager.LoadSceneInPlayMode para contornar Build Profiles vazio em MPPM.");
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    scenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"[GameModeManager] Falha ao carregar '{scenePath}' via EditorSceneManager.LoadSceneInPlayMode: {exception.Message}");
                return false;
            }
        }
#endif

        private static string GetScenePath(string sceneName)
        {
            if (sceneName.StartsWith("Assets/") && sceneName.EndsWith(".unity"))
            {
                return sceneName;
            }

            return "Assets/Cenas/" + sceneName + ".unity";
        }

        private static string GetBuildSceneListForLog()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            if (sceneCount <= 0)
            {
                return "  (nenhuma cena em build settings)";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < sceneCount; i++)
            {
                builder.Append("  ")
                    .Append(i)
                    .Append(": ")
                    .Append(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));

                if (i < sceneCount - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
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
            if (SceneFader.Instance != null)
            {
                yield return SceneFader.Instance.FadeOutRoutine();
            }
            else
            {
                var go = new GameObject("SceneFader");
                var fader = go.AddComponent<SceneFader>();
                yield return fader.FadeOutRoutine();
            }

            yield return MultiplayerRuntimeReset.ResetToOfflineLocal();

            CurrentMode = targetMode;
            _sceneTransitionRoutine = null;
            LoadLocalSceneMppmSafe(sceneName);
        }
    }
}
