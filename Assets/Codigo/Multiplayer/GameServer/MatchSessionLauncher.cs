using UnityEngine;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine.SceneManagement;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Lobby;
using ExoBeasts.Multiplayer.Auth;

#if !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using PlayEveryWare.EpicOnlineServices;
#endif

namespace ExoBeasts.Multiplayer.GameServer
{
    public class MatchSessionLauncher : MonoBehaviour
    {
        private static MatchSessionLauncher _instance;
        public static bool HasInstance => _instance != null;

        public static MatchSessionLauncher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MatchSessionLauncher");
                    _instance = go.AddComponent<MatchSessionLauncher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        public const ushort DEFAULT_PORT = 7777;
        public const string NO_RELAY_CODE = "__NO_RELAY__";

        private Coroutine _pendingClientConnect;

        public static bool IsUsableRelayCode(string relayCode)
        {
            return !string.IsNullOrWhiteSpace(relayCode) &&
                   !string.Equals(relayCode, NO_RELAY_CODE, System.StringComparison.Ordinal);
        }

        private static bool TryGetActiveLobby(string expectedLobbyId, out LobbyInfo activeLobby)
        {
            activeLobby = null;

            if (string.IsNullOrEmpty(expectedLobbyId))
                return false;

            var lobbyManager = LobbyManager.TryGetExistingInstance();
            if (lobbyManager == null || !lobbyManager.IsInLobby())
                return false;

            activeLobby = lobbyManager.GetCurrentLobby();
            return activeLobby != null && activeLobby.lobbyId == expectedLobbyId;
        }

        public void CancelPendingConnect()
        {
            if (_pendingClientConnect != null)
            {
                StopCoroutine(_pendingClientConnect);
                _pendingClientConnect = null;
            }
        }

        /// <summary>
        /// Callback de aprovacao de conexao NGO — roda no Host quando um cliente conecta.
        /// Le o indice do personagem do payload (4 bytes) e registra em CharacterChoiceCache,
        /// para que GameSetupManager possa spawnar o prefab correto.
        /// </summary>
        public void OnNgoConnectionApproval(
            NetworkManager.ConnectionApprovalRequest req,
            NetworkManager.ConnectionApprovalResponse res)
        {
            int charIndex = 0;
            if (req.Payload != null && req.Payload.Length >= 4)
                charIndex = System.BitConverter.ToInt32(req.Payload, 0);

            CharacterChoiceCache.SetClientCharacterIndex(req.ClientNetworkId, charIndex, "MatchSessionLauncher.ConnectionApproval");
            Debug.Log($"[MatchSessionLauncher][DBG] ConnectionApproval recebido: clientId={req.ClientNetworkId} | payloadSize={req.Payload?.Length ?? 0} | charIndex={charIndex}");

            res.Approved = true;
            res.CreatePlayerObject = false; // GameSetupManager instancia manualmente
            res.Pending = false;
        }

        public void LaunchAsHost(
            string mapOverride,
            LobbyInfo currentLobby,
            int hostCharIndex,
            System.Action<string> onError)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[MatchSessionLauncher] NetworkManager.Singleton nulo — prefab NetworkManager ausente da cena?");
                onError?.Invoke("NetworkManager ausente na cena");
                return;
            }

            StartCoroutine(LaunchHostCoroutine(nm, mapOverride, currentLobby, hostCharIndex, onError));
        }

        private System.Collections.IEnumerator LaunchHostCoroutine(
            NetworkManager nm,
            string mapOverride,
            LobbyInfo currentLobby,
            int computedHostCharIndex,
            System.Action<string> onError)
        {
            if (currentLobby == null || string.IsNullOrEmpty(currentLobby.lobbyId))
            {
                Debug.LogError("[MatchSessionLauncher] LaunchAsHost abortado: lobby atual nulo ou sem lobbyId.");
                onError?.Invoke("Lobby atual invalido para iniciar partida");
                yield break;
            }

            string lobbyId = currentLobby.lobbyId;
            string lobbyHostProductUserId = currentLobby.hostProductUserId;
            string fallbackMapName = currentLobby.mapName;
            int maxPlayers = currentLobby.maxPlayers > 0 ? currentLobby.maxPlayers : 1;

#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null)
            {
                Debug.LogError("[MatchSessionLauncher] LaunchAsHost abortado: EOS LobbyInterface indisponivel.");
                onError?.Invoke("EOS nao inicializado");
                yield break;
            }

            var localUserId = GetLocalUserId(lobbyHostProductUserId);
            if (localUserId == null || !localUserId.IsValid())
            {
                Debug.LogError("[MatchSessionLauncher] LaunchAsHost abortado: LocalUserId EOS invalido.");
                onError?.Invoke("Usuario EOS invalido para iniciar partida");
                yield break;
            }
#endif

            if (nm.IsListening || nm.IsHost || nm.IsClient || nm.IsServer)
            {
                Debug.LogWarning($"[MatchSessionLauncher] NGO ja estava em execucao (IsListening={nm.IsListening}, IsHost={nm.IsHost}, IsClient={nm.IsClient}). Shutdown antes de reiniciar...");
                nm.Shutdown();

                float elapsed = 0f;
                while (nm.IsListening && elapsed < 3f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (nm.IsListening)
                {
                    Debug.LogError("[MatchSessionLauncher] Shutdown nao completou em 3s. Abortando LaunchAsHost.");
                    onError?.Invoke("NetworkManager nao encerrou a tempo");
                    yield break;
                }
            }

            bool hasCachedHostChoice = CharacterChoiceCache.TryGet(NetworkManager.ServerClientId, out int cachedHostCharIndex) &&
                                       cachedHostCharIndex >= 0;

            if (hasCachedHostChoice)
            {
                if (computedHostCharIndex >= 0 && computedHostCharIndex != cachedHostCharIndex)
                {
                    Debug.LogWarning(
                        $"[MatchSessionLauncher] Divergencia na escolha do host antes do StartMatch. " +
                        $"Cache={cachedHostCharIndex} | Computado={computedHostCharIndex}. " +
                        "Preservando o valor ja registrado no cache para evitar spawn do prefab errado.");
                }
                else
                {
                    Debug.Log($"[MatchSessionLauncher] Host charIndex preservado do cache: {cachedHostCharIndex}");
                }
            }
            else
            {
                int resolvedHostCharIndex = computedHostCharIndex >= 0 ? computedHostCharIndex : 0;
                CharacterChoiceCache.SetHostCharacterIndex(resolvedHostCharIndex, "MatchSessionLauncher.LaunchAsHostFallback");
                Debug.Log($"[MatchSessionLauncher] Host charIndex cacheado por fallback: {resolvedHostCharIndex}");
            }

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = OnNgoConnectionApproval;

            ushort port = DEFAULT_PORT;
            string localIp = null;
            string relayJoinCode = null;
            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[MatchSessionLauncher] UnityTransport nao encontrado no NetworkManager! Adicione o componente.");
                onError?.Invoke("UnityTransport ausente");
                yield break;
            }

            if (nm.NetworkConfig.NetworkTransport == null)
            {
                Debug.LogWarning("[MatchSessionLauncher] NetworkConfig.NetworkTransport era null — atribuindo via GetComponent.");
                nm.NetworkConfig.NetworkTransport = transport;
            }

#if UNITY_EDITOR
            localIp = "127.0.0.1";
            transport.SetConnectionData("0.0.0.0", port);
            port = transport.ConnectionData.Port;
            Debug.Log($"[MatchSessionLauncher][DBG] MPPM — listen 0.0.0.0:{port}, publicando IP: {localIp}");
#else
            float ugsWait = 0f;
            while ((UGSBootstrap.Instance == null || !UGSBootstrap.Instance.IsReady) && ugsWait < 10f)
            {
                ugsWait += Time.deltaTime;
                yield return null;
            }

            if (UGSBootstrap.Instance != null && UGSBootstrap.Instance.IsReady)
            {
                var allocTask = RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                yield return new WaitUntil(() => allocTask.IsCompleted);
                if (!allocTask.IsFaulted && allocTask.Result != null)
                {
                    Allocation relayAlloc = allocTask.Result;
                    var codeTask = RelayService.Instance.GetJoinCodeAsync(relayAlloc.AllocationId);
                    yield return new WaitUntil(() => codeTask.IsCompleted);
                    if (!codeTask.IsFaulted)
                    {
                        relayJoinCode = codeTask.Result;
                        transport.SetHostRelayData(
                            relayAlloc.RelayServer.IpV4,
                            (ushort)relayAlloc.RelayServer.Port,
                            relayAlloc.AllocationIdBytes,
                            relayAlloc.Key,
                            relayAlloc.ConnectionData);
                        Debug.Log($"[MatchSessionLauncher] Relay alocado. JoinCode={relayJoinCode}");
                    }
                    else
                    {
                        Debug.LogWarning($"[MatchSessionLauncher] GetJoinCodeAsync falhou: {codeTask.Exception?.Message}. Fallback IP direto.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[MatchSessionLauncher] CreateAllocationAsync falhou: {allocTask.Exception?.Message}. Fallback IP direto.");
                }
            }
            else
            {
                Debug.LogWarning("[MatchSessionLauncher] UGSBootstrap nao pronto no timeout. Fallback para IP direto.");
            }

            if (string.IsNullOrEmpty(relayJoinCode))
            {
                localIp = LobbyManager.GetLocalIpAddress();
                transport.SetConnectionData("0.0.0.0", port);
                port = transport.ConnectionData.Port;
                Debug.Log($"[MatchSessionLauncher][DBG] Fallback IP direto: listen 0.0.0.0:{port}, publicando: {localIp}");
            }
#endif

            if (!TryGetActiveLobby(lobbyId, out _))
            {
                Debug.LogWarning($"[MatchSessionLauncher] StartMatch cancelado antes do StartHost: lobby '{lobbyId}' nao esta mais ativo.");
                yield break;
            }

            CancelPendingConnect();

            if (nm.IsListening || nm.IsClient || nm.IsHost || nm.IsServer)
            {
                Debug.LogWarning($"[MatchSessionLauncher] Estado inesperado pre-StartHost. Shutdown emergencial...");
                nm.Shutdown();
                float elapsed2 = 0f;
                while (nm.IsListening && elapsed2 < 2f) { elapsed2 += Time.deltaTime; yield return null; }
                if (nm.IsListening)
                {
                    Debug.LogError("[MatchSessionLauncher] Shutdown pre-StartHost nao completou em 2s. Abortando.");
                    onError?.Invoke("NGO nao encerrou antes de StartHost");
                    yield break;
                }
            }

            Debug.Log($"[MatchSessionLauncher] Tentando StartHost: transport={transport.Protocol}, approval={nm.NetworkConfig.ConnectionApproval}");

            if (!nm.StartHost())
            {
                Debug.LogError($"[MatchSessionLauncher] StartHost retornou false.");
                onError?.Invoke("Falha ao iniciar Host NGO");
                yield break;
            }

            Debug.Log($"[MatchSessionLauncher] Host NGO ativo. Publicando no lobby: {localIp}:{port}");

#if !EOS_DISABLE
            var modOpts = new UpdateLobbyModificationOptions
            {
                LocalUserId = localUserId,
                LobbyId = lobbyId,
            };

            var modificationResult = lobbyInterface.UpdateLobbyModification(ref modOpts, out var mod);
            if (modificationResult != Result.Success)
            {
                Debug.LogError($"[MatchSessionLauncher] Falha ao obter LobbyModification para StartMatch: {modificationResult}");
                onError?.Invoke($"Falha ao preparar lobby para iniciar partida: {modificationResult}");
                nm.Shutdown();
                yield break;
            }

            bool scheduled = false;
            try
            {
                string relayCodeToPublish = !string.IsNullOrEmpty(relayJoinCode) ? relayJoinCode : NO_RELAY_CODE;
                AddStringAttr(mod, LobbyAttributes.RELAY_CODE, relayCodeToPublish, LobbyAttributeVisibility.Public);

                string publishIp = localIp ?? LobbyManager.GetLocalIpAddress();
                AddStringAttr(mod, LobbyAttributes.SERVER_ADDRESS, publishIp, LobbyAttributeVisibility.Public);
                AddInt64Attr(mod, LobbyAttributes.SERVER_PORT, port, LobbyAttributeVisibility.Public);
                AddStringAttr(mod, LobbyAttributes.LOBBY_STATE, LobbyState.InGame.ToString(), LobbyAttributeVisibility.Public);

                Debug.Log($"[MatchSessionLauncher] Publicando atributos: RELAY_CODE='{relayCodeToPublish}' | SERVER_ADDRESS='{publishIp}' | PORT={port}");

                string capturedMapOverride = mapOverride;
                var updateOpts = new UpdateLobbyOptions { LobbyModificationHandle = mod };
                lobbyInterface.UpdateLobby(ref updateOpts, null, (ref UpdateLobbyCallbackInfo info) =>
                {
                    mod.Release();
                    if (info.ResultCode == Result.Success)
                    {
                        if (!TryGetActiveLobby(lobbyId, out var activeLobby))
                        {
                            Debug.LogWarning($"[MatchSessionLauncher] UpdateLobby retornou apos o lobby '{lobbyId}' deixar de estar ativo. Abortando carregamento da partida.");
                            if (nm != null && nm.IsHost)
                                nm.Shutdown();
                            return;
                        }

                        string sceneName = !string.IsNullOrEmpty(capturedMapOverride) ? capturedMapOverride : activeLobby.mapName;
                        if (string.IsNullOrEmpty(sceneName))
                            sceneName = fallbackMapName;

                        int expectedPlayers = activeLobby.currentPlayers > 0 ? activeLobby.currentPlayers : 1;
                        Debug.Log($"[MatchSessionLauncher] Atributos publicados. Aguardando {expectedPlayers} jogador(es) conectarem ao NGO antes de carregar '{sceneName}'...");
                        StartCoroutine(WaitForAllClientsAndLoadScene(sceneName, expectedPlayers, lobbyId, onError));
                    }
                    else
                    {
                        Debug.LogError($"[MatchSessionLauncher] Falha ao publicar endereco: {info.ResultCode}");
                        onError?.Invoke($"Falha ao iniciar partida: {info.ResultCode}");
                        NetworkManager.Singleton?.Shutdown();
                    }
                });
                scheduled = true;
            }
            finally
            {
                if (!scheduled) mod.Release();
            }
#endif
        }

        /// <summary>
        /// Aguarda todos os clients esperados conectarem ao NGO antes de carregar a cena.
        /// Elimina a race condition do delay fixo: cada client passa por EOS propagation +
        /// UGS + JoinAllocationAsync + handshake NGO, o que leva 5-15s em builds com Relay.
        /// Timeout de 25s como fallback para nao travar o host se um client desconectar.
        /// </summary>
        private System.Collections.IEnumerator WaitForAllClientsAndLoadScene(
            string sceneName, int expectedPlayerCount, string lobbyId, System.Action<string> onError)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsHost)
            {
                Debug.LogWarning("[MatchSessionLauncher] WaitForAllClientsAndLoadScene abortado na entrada: nao e Host.");
                yield break;
            }

            if (!TryGetActiveLobby(lobbyId, out _))
            {
                Debug.LogWarning($"[MatchSessionLauncher] WaitForAllClientsAndLoadScene abortado: lobby '{lobbyId}' nao esta mais ativo.");
                nm.Shutdown();
                yield break;
            }

            Debug.Log($"[MatchSessionLauncher] Aguardando {expectedPlayerCount} jogador(es) antes de carregar '{sceneName}'...");

            const float timeoutSeconds = 25f;
            float elapsed = 0f;
            float nextLogAt = 1f;

            // ConnectedClientsIds inclui o proprio host (ServerClientId=0), entao a
            // contagem alvo ja cobre todos os membros do lobby sem ajuste adicional.
            System.Action<ulong> onClientConnected = (clientId) =>
            {
                Debug.Log($"[MatchSessionLauncher] Client conectou: clientId={clientId} | " +
                          $"Total={nm.ConnectedClientsIds.Count}/{expectedPlayerCount}");
            };

            nm.OnClientConnectedCallback += onClientConnected;

            try
            {
                while (true)
                {
                    if (nm == null || !nm.IsHost)
                    {
                        Debug.LogWarning("[MatchSessionLauncher] WaitForAllClientsAndLoadScene cancelado: nao e mais Host.");
                        yield break;
                    }

                    if (!TryGetActiveLobby(lobbyId, out _))
                    {
                        Debug.LogWarning($"[MatchSessionLauncher] WaitForAllClientsAndLoadScene cancelado: lobby '{lobbyId}' foi limpo ou trocado.");
                        nm.Shutdown();
                        yield break;
                    }

                    int connected = nm.ConnectedClientsIds.Count;

                    if (elapsed >= nextLogAt)
                    {
                        Debug.Log($"[MatchSessionLauncher] Aguardando clients... {connected}/{expectedPlayerCount} ({elapsed:F1}s / {timeoutSeconds}s)");
                        nextLogAt += 1f;
                    }

                    if (connected >= expectedPlayerCount)
                    {
                        Debug.Log($"[MatchSessionLauncher] Todos os {connected} jogadores conectados. Carregando '{sceneName}'.");
                        break;
                    }

                    if (elapsed >= timeoutSeconds)
                    {
                        Debug.LogWarning($"[MatchSessionLauncher] Timeout {timeoutSeconds}s atingido. Carregando '{sceneName}' com {connected}/{expectedPlayerCount} jogadores.");
                        break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                if (nm != null)
                    nm.OnClientConnectedCallback -= onClientConnected;
            }

            if (nm == null || !nm.IsHost)
            {
                Debug.LogWarning("[MatchSessionLauncher] WaitForAllClientsAndLoadScene abortado antes do LoadScene: host inativo.");
                yield break;
            }

            if (!TryGetActiveLobby(lobbyId, out _))
            {
                Debug.LogWarning($"[MatchSessionLauncher] LoadScene abortado: lobby '{lobbyId}' nao esta mais ativo.");
                nm.Shutdown();
                yield break;
            }

            // BUG FIX (2026-05-21): pre-validar que a cena resolve via build index ANTES de
            // chamar nm.SceneManager.LoadScene. Em MPPM clones, EditorBuildSettings pode estar
            // dessincronizado entre o original e o clone — o servidor pode resolver mas o
            // cliente nao. Sem este check, o cliente fica preso em loading ate o watchdog
            // de 15s do SceneTransitionHandler disparar.
            // Pareado com BuildSceneListGuard (Editor-time) e SceneManager.VerifySceneBeforeLoading (runtime).
            string scenePathForValidation = sceneName.StartsWith("Assets/") && sceneName.EndsWith(".unity")
                ? sceneName
                : "Assets/Scenes/" + sceneName + ".unity";
            int validationIndex = SceneUtility.GetBuildIndexByScenePath(scenePathForValidation);
            if (validationIndex < 0)
            {
                Debug.LogError(
                    $"[MatchSessionLauncher] Cena '{sceneName}' nao resolve via SceneUtility.GetBuildIndexByScenePath('{scenePathForValidation}'). " +
                    "Build Settings provavelmente dessincronizadas. " +
                    "No Editor, executar Tools > ExoBeasts > Repair Build Scene List.");
                onError?.Invoke($"Cena '{sceneName}' nao esta na Build Settings deste processo");
                yield break;
            }
            Debug.Log($"[MatchSessionLauncher] Cena '{sceneName}' resolve para buildIndex={validationIndex}. Prosseguindo com LoadScene via NGO.");

            // BUG FIX (2026-05-21): registrar VerifySceneBeforeLoading no NGO SceneManager
            // para diagnostico — em cada cliente, este callback dispara antes do load real.
            // Se retornar false, o NGO aborta o load do lado do cliente, evitando o estado
            // pendurado que dispara o watchdog. Aqui sempre retornamos true (sem bloqueio),
            // mas logamos o nome+index recebidos para confirmar que a entrega via rede esta OK.
            nm.SceneManager.VerifySceneBeforeLoading = OnVerifySceneBeforeLoading;

            var status = nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[MatchSessionLauncher] LoadScene('{sceneName}') falhou com status: {status}. " +
                               "Verifique Build Settings e EnableSceneManagement=true no NetworkManager.");
                onError?.Invoke($"Falha ao carregar cena '{sceneName}': {status}");
            }
        }

        /// <summary>
        /// VerifySceneBeforeLoading do NGO 1.12: dispara em cada peer (host e clientes)
        /// quando um SceneEventType.Load chega via rede. Retornar false aborta o load no
        /// peer atual com erro claro em vez de deixar o Unity falhar silenciosamente.
        ///
        /// Diagnostico para MPPM: se em um clone este callback dispara com sceneName valido
        /// mas o LoadSceneAsync subsequente falha, confirma que o problema esta na lista de
        /// cenas do clone (EditorBuildSettings dessincronizadas) e nao no payload de rede.
        /// </summary>
        private static bool OnVerifySceneBeforeLoading(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            string activeScenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
            bool isResolvable = !string.IsNullOrEmpty(activeScenePath);
            Debug.Log(
                $"[MatchSessionLauncher.VerifySceneBeforeLoading] sceneIndex={sceneIndex} | " +
                $"sceneName='{sceneName}' | mode={loadSceneMode} | " +
                $"resolvedPath='{activeScenePath}' | isResolvable={isResolvable} | " +
                $"sceneCountInBuild={SceneManager.sceneCountInBuildSettings}");

            if (!isResolvable)
            {
                Debug.LogError(
                    $"[MatchSessionLauncher.VerifySceneBeforeLoading] Abortando load: sceneIndex={sceneIndex} " +
                    $"nao resolve para nenhum path neste processo. Provavel desync EditorBuildSettings em MPPM clone. " +
                    $"Total cenas neste processo: {SceneManager.sceneCountInBuildSettings}.");
                return false;
            }

            return true;
        }

        public void ConnectAsClientViaIp(string serverAddress, ushort port, int myCharIndex, System.Action<string> onError)
        {
            var nmClient = NetworkManager.Singleton;
            var transport = nmClient?.GetComponent<UnityTransport>();
            if (nmClient != null && transport != null)
            {
                CancelPendingConnect();
                _pendingClientConnect = StartCoroutine(ConnectClientCoroutine(nmClient, transport, serverAddress, port, myCharIndex, onError));
            }
        }

        public void ConnectAsClientViaRelay(string relayCode, int myCharIndex, System.Action<string> onError)
        {
            var nmClientRelay = NetworkManager.Singleton;
            var transportRelay = nmClientRelay?.GetComponent<UnityTransport>();
            if (nmClientRelay != null && transportRelay != null)
            {
                CancelPendingConnect();
                _pendingClientConnect = StartCoroutine(ConnectClientViaRelayCoroutine(nmClientRelay, transportRelay, relayCode, myCharIndex, onError));
            }
        }

        private System.Collections.IEnumerator ConnectClientCoroutine(
            NetworkManager nmClient, UnityTransport transport, string serverAddress, ushort port, int myCharIndex, System.Action<string> onError)
        {
            Debug.Log($"[MatchSessionLauncher][DBG] ConnectClientCoroutine iniciada: target={serverAddress}:{port} | IsListening={nmClient.IsListening} | IsClient={nmClient.IsClient}");

            if (nmClient.IsListening || nmClient.IsClient || nmClient.IsHost)
            {
                Debug.LogWarning("[MatchSessionLauncher] Cliente: NGO ja em execucao. Shutdown antes de reconectar...");
                nmClient.Shutdown();

                float elapsed = 0f;
                while (nmClient.IsListening && elapsed < 3f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (nmClient.IsListening)
                {
                    Debug.LogError("[MatchSessionLauncher] Cliente: Shutdown nao completou em 3s. Abortando conexao.");
                    onError?.Invoke("NetworkManager do cliente nao encerrou a tempo");
                    yield break;
                }
            }

            if (nmClient.NetworkConfig.NetworkTransport == null && transport != null)
            {
                Debug.LogWarning("[MatchSessionLauncher] NetworkConfig.NetworkTransport era null — atribuindo via GetComponent.");
                nmClient.NetworkConfig.NetworkTransport = transport;
            }

            if (nmClient.NetworkConfig.NetworkTransport == null)
            {
                Debug.LogError("[MatchSessionLauncher] UnityTransport nao encontrado no NetworkManager.");
                onError?.Invoke("UnityTransport ausente no cliente");
                yield break;
            }

            transport.SetConnectionData(serverAddress, port);
            Debug.Log($"[MatchSessionLauncher][DBG] Transport configurado: {transport.ConnectionData.Address}:{transport.ConnectionData.Port}");

            nmClient.NetworkConfig.ConnectionApproval = true;
            nmClient.NetworkConfig.ConnectionData = System.BitConverter.GetBytes(myCharIndex);
            Debug.Log($"[MatchSessionLauncher] Enviando charIndex={myCharIndex} no payload de conexao");

            bool clientStarted = nmClient.StartClient();
            Debug.Log($"[MatchSessionLauncher][DBG] StartClient retornou: {clientStarted} | IsClient={nmClient.IsClient} | IsListening={nmClient.IsListening}");
            if (!clientStarted)
            {
                Debug.LogError($"[MatchSessionLauncher] StartClient retornou false. IsListening={nmClient.IsListening}, IsClient={nmClient.IsClient}");
                onError?.Invoke("Falha ao iniciar Client NGO");
            }
        }

        private System.Collections.IEnumerator ConnectClientViaRelayCoroutine(
            NetworkManager nmClient, UnityTransport transport, string joinCode, int myCharIndex, System.Action<string> onError)
        {
            Debug.Log($"[MatchSessionLauncher] ConnectClientViaRelayCoroutine iniciada — joinCode={joinCode}");

            if (nmClient.IsListening || nmClient.IsClient || nmClient.IsHost)
            {
                Debug.LogWarning("[MatchSessionLauncher] Cliente: NGO ja em execucao. Shutdown antes de conectar via Relay...");
                nmClient.Shutdown();
                float elapsed = 0f;
                while (nmClient.IsListening && elapsed < 3f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (nmClient.IsListening)
                {
                    Debug.LogError("[MatchSessionLauncher] Cliente: Shutdown nao completou em 3s. Abortando conexao Relay.");
                    onError?.Invoke("NetworkManager do cliente nao encerrou a tempo");
                    yield break;
                }
            }

            if (nmClient.NetworkConfig.NetworkTransport == null && transport != null)
                nmClient.NetworkConfig.NetworkTransport = transport;

#if !UNITY_EDITOR
            float ugsWait = 0f;
            while ((UGSBootstrap.Instance == null || !UGSBootstrap.Instance.IsReady) && ugsWait < 10f)
            {
                ugsWait += Time.deltaTime;
                yield return null;
            }

            if (UGSBootstrap.Instance == null || !UGSBootstrap.Instance.IsReady)
            {
                Debug.LogError("[MatchSessionLauncher] UGS nao pronto — verifique Project ID no Unity Dashboard");
                onError?.Invoke("UGS nao inicializado");
                yield break;
            }

            JoinAllocation join = null;
            float[] relayBackoff = { 1f, 2f };

            for (int attempt = 0; attempt <= relayBackoff.Length; attempt++)
            {
                if (attempt > 0)
                {
                    Debug.LogWarning($"[MatchSessionLauncher] Relay retry ({attempt}/{relayBackoff.Length}) em {relayBackoff[attempt - 1]}s...");
                    yield return new WaitForSeconds(relayBackoff[attempt - 1]);
                }

                var joinTask = RelayService.Instance.JoinAllocationAsync(joinCode);
                yield return new WaitUntil(() => joinTask.IsCompleted);

                if (!joinTask.IsFaulted)
                {
                    join = joinTask.Result;
                    break;
                }

                Debug.LogError($"[MatchSessionLauncher] JoinAllocationAsync falhou (tentativa {attempt + 1}): {joinTask.Exception?.GetBaseException().Message}");

                if (attempt == relayBackoff.Length)
                {
                    onError?.Invoke("Falha ao entrar na alocacao Relay apos todas as tentativas");
                    yield break;
                }
            }
            transport.SetClientRelayData(
                join.RelayServer.IpV4,
                (ushort)join.RelayServer.Port,
                join.AllocationIdBytes,
                join.Key,
                join.ConnectionData,
                join.HostConnectionData);
#else
            // Editor code fallback or warning if relay used in editor incorrectly
            yield return null;
#endif

            nmClient.NetworkConfig.ConnectionApproval = true;
            nmClient.NetworkConfig.ConnectionData = System.BitConverter.GetBytes(myCharIndex);
            Debug.Log($"[MatchSessionLauncher] Relay configurado. Enviando charIndex={myCharIndex}. Iniciando StartClient...");

            bool clientStarted = nmClient.StartClient();
            Debug.Log($"[MatchSessionLauncher] StartClient (Relay) retornou: {clientStarted}");
            if (!clientStarted)
                onError?.Invoke("Falha ao iniciar Client NGO via Relay");
        }

#if !EOS_DISABLE
        private LobbyInterface GetLobbyInterface() => EOSManager.Instance.GetEOSPlatformInterface()?.GetLobbyInterface();

        private ProductUserId GetLocalUserId(string hostProductUserId)
        {
            string userIdStr = SessionManager.Instance?.GetUserId();

            if (!string.IsNullOrEmpty(userIdStr) &&
                !string.IsNullOrEmpty(hostProductUserId) &&
                hostProductUserId != userIdStr)
            {
                Debug.LogError($"[MatchSessionLauncher] Usuario local ({userIdStr}) nao e host do lobby ({hostProductUserId}).");
                return null;
            }

            if (string.IsNullOrEmpty(userIdStr))
                userIdStr = hostProductUserId;

            if (string.IsNullOrEmpty(userIdStr))
                return null;

            try
            {
                return ProductUserId.FromString(userIdStr);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[MatchSessionLauncher] ProductUserId local invalido: {exception.Message}");
                return null;
            }
        }

        private static void AddStringAttr(LobbyModification mod, string key, string value, LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddAttributeOptions
            {
                Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsUtf8 = value } },
                Visibility = vis,
            };
            mod.AddAttribute(ref opts);
        }

        private static void AddInt64Attr(LobbyModification mod, string key, long value, LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddAttributeOptions
            {
                Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsInt64 = value } },
                Visibility = vis,
            };
            mod.AddAttribute(ref opts);
        }
#endif
    }
}
