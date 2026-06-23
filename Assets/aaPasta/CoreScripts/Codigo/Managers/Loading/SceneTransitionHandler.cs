using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using ExoBeasts.Multiplayer.Lobby;

namespace ExoBeasts.Managers.Loading
{
    /// <summary>
    /// Escuta os eventos de troca de cena do Netcode for GameObjects (NGO) e gerencia 
    /// a aparição e o desaparecimento da tela de Loading automaticamente.
    /// Deve ser anexado no mesmo objeto do LoadingScreenUI.
    /// </summary>
    public class SceneTransitionHandler : MonoBehaviour
    {
        private static SceneTransitionHandler instance;
        private const string HIDE_MSG = "HideLoadingScreen";
        private const string LOBBY_SCENE_NAME = "LobbyScene";
        private const string NETWORK_FAILURE_MESSAGE = "Conexao multiplayer perdida. Voltando ao lobby.";
        private const float LOADING_WATCHDOG_SECONDS = 15f;
        private bool handlingNetworkFailure;
        private Coroutine loadingWatchdogCoroutine;
        private string currentLoadingSceneName;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                // O DontDestroyOnLoad já é feito no LoadingScreenUI, mas não machuca garantir.
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Aguardamos o NetworkManager estar totalmente vivo para conectar os eventos
            StartCoroutine(WaitForNetworkManager());
        }

        private System.Collections.IEnumerator WaitForNetworkManager()
        {
            while (NetworkManager.Singleton == null)
            {
                yield return null;
            }

            NetworkManager.Singleton.OnServerStarted += SubscribeToSceneEvents;
            NetworkManager.Singleton.OnClientStarted += SubscribeToSceneEvents;
            SubscribeToNetworkFailureEvents();
            
            // Se o jogo já tiver iniciado (o NetworkManager já subiu)
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
            {
                SubscribeToSceneEvents();
            }
        }

        private void SubscribeToSceneEvents()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                // Previne inscrições duplicadas
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
                NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
            }

            // Registrar para ouvir a mensagem de "pode apagar a tela" (funciona para clientes e servidor)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(HIDE_MSG);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(HIDE_MSG, OnReceiveHideMessage);
            }

            SubscribeToNetworkFailureEvents();
        }

        private void SubscribeToNetworkFailureEvents()
        {
            if (NetworkManager.Singleton == null)
                return;

            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
            NetworkManager.Singleton.OnClientStopped -= OnNetworkStopped;
            NetworkManager.Singleton.OnClientStopped += OnNetworkStopped;
            NetworkManager.Singleton.OnServerStopped -= OnNetworkStopped;
            NetworkManager.Singleton.OnServerStopped += OnNetworkStopped;
        }

        private void OnDestroy()
        {
            StopLoadingWatchdog();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= SubscribeToSceneEvents;
                NetworkManager.Singleton.OnClientStarted -= SubscribeToSceneEvents;
                NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
                NetworkManager.Singleton.OnClientStopped -= OnNetworkStopped;
                NetworkManager.Singleton.OnServerStopped -= OnNetworkStopped;
                
                if (NetworkManager.Singleton.CustomMessagingManager != null)
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(HIDE_MSG);
            }
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                // Início do carregamento da cena (Disparado para todo mundo)
                case SceneEventType.Load:
                    currentLoadingSceneName = sceneEvent.SceneName;
                    if (LoadingScreenUI.Instance != null)
                    {
                        LoadingScreenUI.Instance.Show();
                    }
                    StartLoadingWatchdog();
                    break;

                // Disparado no Servidor/Host quando TODOS os clientes terminarem de baixar/carregar a cena!
                // É o "Sinal Verde" oficial do multiplayer.
                case SceneEventType.LoadEventCompleted:
                    StopLoadingWatchdog();
                    if (NetworkManager.Singleton.IsServer)
                    {
                        HideLoadingForEveryone();
                    }
                    else
                    {
                        HideLoadingLocal();
                    }
                    break;

                case SceneEventType.LoadComplete:
                case SceneEventType.SynchronizeComplete:
                    if (!NetworkManager.Singleton.IsServer)
                    {
                        HideLoadingLocal();
                    }
                    break;
            }
        }

        private void OnTransportFailure()
        {
            TryRecoverLoadingFailure("transport failure");
        }

        private void OnNetworkStopped(bool wasHost)
        {
            TryRecoverLoadingFailure(wasHost ? "host stopped" : "client stopped");
        }

        private void TryRecoverLoadingFailure(string reason)
        {
            if (handlingNetworkFailure)
                return;

            if (GameModeManager.CurrentMode != GameMode.Multiplayer)
                return;

            LoadingScreenUI loading = LoadingScreenUI.Instance;
            if (loading == null || !loading.IsVisible)
                return;

            Debug.LogWarning($"[SceneTransitionHandler] Falha de rede durante loading ({reason}). Retornando para LobbyScene.");
            StartCoroutine(ReturnToLobbyAfterNetworkFailure());
        }

        private System.Collections.IEnumerator ReturnToLobbyAfterNetworkFailure()
        {
            handlingNetworkFailure = true;

            if (LoadingScreenUI.Instance != null)
                LoadingScreenUI.Instance.ForceHide();

            StopLoadingWatchdog();

            LobbyManager.TryGetExistingInstance()?.ForceResetRuntimeState(false);
            GameModeManager.ReturnToMultiplayerLobby();

            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.IsClient || nm.IsHost || nm.IsServer))
            {
                nm.Shutdown();

                float elapsed = 0f;
                while (nm.IsListening && elapsed < 3f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            global::LobbySceneUI.SetPendingStatusMessage(NETWORK_FAILURE_MESSAGE);
            SceneManager.LoadScene(LOBBY_SCENE_NAME, LoadSceneMode.Single);
            handlingNetworkFailure = false;
        }

        private void HideLoadingLocal()
        {
            StopLoadingWatchdog();
            if (LoadingScreenUI.Instance != null && LoadingScreenUI.Instance.IsVisible)
                LoadingScreenUI.Instance.Hide();
        }

        private void StartLoadingWatchdog()
        {
            StopLoadingWatchdog();
            loadingWatchdogCoroutine = StartCoroutine(LoadingWatchdogCoroutine());
        }

        private void StopLoadingWatchdog()
        {
            if (loadingWatchdogCoroutine != null)
            {
                StopCoroutine(loadingWatchdogCoroutine);
                loadingWatchdogCoroutine = null;
            }
        }

        private System.Collections.IEnumerator LoadingWatchdogCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < LOADING_WATCHDOG_SECONDS)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;

                if (LoadingScreenUI.Instance == null || !LoadingScreenUI.Instance.IsVisible)
                {
                    loadingWatchdogCoroutine = null;
                    yield break;
                }
            }

            loadingWatchdogCoroutine = null;

            if (LoadingScreenUI.Instance == null || !LoadingScreenUI.Instance.IsVisible)
                yield break;

            NetworkManager nm = NetworkManager.Singleton;
            bool networkHealthy = nm != null && nm.IsListening && (nm.IsServer || nm.IsClient || nm.IsHost);
            bool targetSceneLoaded = string.IsNullOrEmpty(currentLoadingSceneName) ||
                                     SceneManager.GetActiveScene().name == currentLoadingSceneName;

            if (networkHealthy && targetSceneLoaded)
            {
                Debug.LogWarning($"[SceneTransitionHandler] Watchdog escondeu loading apos {LOADING_WATCHDOG_SECONDS}s em cena '{SceneManager.GetActiveScene().name}'.");
                LoadingScreenUI.Instance.ForceHide();
                yield break;
            }

            Debug.LogWarning($"[SceneTransitionHandler] Watchdog detectou loading preso. Cena alvo='{currentLoadingSceneName}', cena ativa='{SceneManager.GetActiveScene().name}'.");
            TryRecoverLoadingFailure("loading watchdog timeout");
        }

        private void HideLoadingForEveryone()
        {
            // Manda um ping sem payload via CustomMessagingManager para a rede inteira desligar a tela.
            // (Assim evitamos criar um NetworkBehaviour só pra desligar a tela)
            using FastBufferWriter writer = new FastBufferWriter(0, Unity.Collections.Allocator.Temp);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(HIDE_MSG, writer);
            
            // O Host esconde para si mesmo imediatamente
            if (LoadingScreenUI.Instance != null)
            {
                LoadingScreenUI.Instance.Hide();
            }
        }

        // Chamado nos clientes quando recebem a mensagem do Host
        private void OnReceiveHideMessage(ulong senderClientId, FastBufferReader messagePayload)
        {
            // Checagem de segurança, só aceita comando de desligar do Servidor (ClientId 0)
            if (senderClientId == NetworkManager.ServerClientId)
            {
                if (LoadingScreenUI.Instance != null)
                {
                    LoadingScreenUI.Instance.Hide();
                }
            }
        }
    }
}
