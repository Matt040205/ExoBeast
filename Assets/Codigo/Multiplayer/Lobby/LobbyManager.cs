using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine.SceneManagement;

#if !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
#endif

using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Core;

namespace ExoBeasts.Multiplayer.Lobby
{
    /// <summary>
    /// ── LobbyManager ─────────────────────────────────────
    /// Gerencia operacoes de lobby via Epic Online Services.
    ///
    ///  ▸ CreateLobby → SetLobbyAttributes → SetMemberAttribute(DISPLAY_NAME)
    ///  ▸ SearchLobbies → cache de LobbyDetails → JoinLobby (ou SearchByIdThenJoin)
    ///  ▸ StartMatch: publica SERVER_ADDRESS; clientes detectam via OnLobbyAttributeUpdated
    ///  ▸ Notificacoes EOS: MemberStatus, LobbyUpdate, MemberUpdate (IS_READY)
    ///  ▸ _detailsCache: EOS exige LobbyDetails handle, nao apenas string ID
    ///  ▸ Singleton com DontDestroyOnLoad
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        private static LobbyManager _instance;
        public static bool HasInstance => _instance != null;
        public static LobbyManager TryGetExistingInstance() => _instance;

        public static LobbyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("LobbyManager");
                    _instance = go.AddComponent<LobbyManager>();
                }
                return _instance;
            }
        }

        public event Action<LobbyInfo> OnLobbyCreated;
        public event Action<List<LobbyInfo>> OnLobbiesFound;
        public event Action<LobbyInfo> OnLobbyJoined;
        public event Action OnLobbyLeft;
        public event Action<LobbyMember> OnMemberJoined;
        public event Action<LobbyMember> OnMemberLeft;
        public event Action<LobbyMember> OnMemberUpdated;
        public event Action<string> OnError;

        private LobbyInfo _currentLobby;
        private bool _isInLobby;
        internal LobbyMembershipService _membershipService;

        // EOS exige LobbyDetails handle para JoinLobby, nao apenas string ID
        private readonly Dictionary<string, LobbyDetails> _detailsCache =
            new Dictionary<string, LobbyDetails>();

        // OPTIMIZATION (Sprint 3 / Item A3 - 2026-05-08): rate-limit + cache de SearchLobbies.
        // EOS Lobby Service tem rate limit por usuario. Sem cooldown, usuario clicando
        // "Buscar" repetidamente disparava varias requests simultaneas, desperdicando
        // banda e aumentando o risco de Result.RateLimited.
        private const float SEARCH_LOBBIES_COOLDOWN_SECONDS = 2f;
        private float _lastSearchLobbiesTime = -10f;
        private List<LobbyInfo> _lastSearchLobbiesResult; // null antes da primeira busca

        private LobbyNotificationDispatcher _dispatcher;

        internal void InvokeOnMemberJoined(LobbyMember m) => OnMemberJoined?.Invoke(m);
        internal void InvokeOnMemberLeft(LobbyMember m) => OnMemberLeft?.Invoke(m);
        internal void InvokeOnMemberUpdated(LobbyMember m) => OnMemberUpdated?.Invoke(m);
        internal void InvokeOnLobbyLeft() => OnLobbyLeft?.Invoke();
        internal void InvokeOnError(string e) => OnError?.Invoke(e);

        // Cache do EOSManagerWrapper para evitar lazy-create em OnDestroy
        private Core.EOSManagerWrapper _eosCache;

        // Coroutine de conexao cliente em andamento — cancelada se StartMatch for chamado no host


        // OPTIMIZATION (Sprint 4 / Item A6 - 2026-05-21): debounce de SetMemberAttribute.
        // EOS Lobby Service tem rate limit (~30 calls/min). UI hesitante (jogador trocando
        // personagem rapidamente) disparava varios UpdateLobbyMember consecutivos.
        // Antes: cada SetMemberAttribute -> chamada EOS imediata.
        // Agora: chamadas para mesma key dentro de 250ms colapsam em uma unica call com ultimo valor.
        // Sem isso: ate 5 EOS calls por hesitacao tipica, risco de Result.RateLimited e
        // callbacks redundantes em outros clientes.
        // Dictionary<key, Coroutine> garante que cada atributo (IS_READY, CHARACTER_INDEX, ...)
        // tem seu proprio debounce — trocar Ready nao reseta timer da troca de personagem.
        private const float SET_MEMBER_ATTRIBUTE_DEBOUNCE_SECONDS = 0.25f;
        private readonly Dictionary<string, string> _pendingMemberAttributes = new Dictionary<string, string>();
        private readonly Dictionary<string, Coroutine> _memberAttributeDebounceCoroutines = new Dictionary<string, Coroutine>();

        private const ushort DEFAULT_PORT = 7777;
        private const string BUCKET_ID = "ExoBeasts";
        private const string NO_RELAY_CODE = "__NO_RELAY__";

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            transform.SetParent(null); // DDOL requer root GameObject
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // EOS pode ainda nao estar inicializado (init assincrona via coroutine no EOSManagerWrapper)
            _eosCache = Core.EOSManagerWrapper.Instance;
            _membershipService = new LobbyMembershipService(this);
            _dispatcher = new LobbyNotificationDispatcher(this);
            if (_eosCache.IsInitialized)
                _dispatcher.RegisterNotifications();
            else
                _eosCache.OnEOSInitialized += _dispatcher.RegisterNotifications;
        }

        private void OnDestroy()
        {
            if (_eosCache != null && _dispatcher != null)
                _eosCache.OnEOSInitialized -= _dispatcher.RegisterNotifications;
            _dispatcher?.UnregisterNotifications();
            ReleaseDetailCache();

            // OPTIMIZATION (Sprint 4 / Item A6): cancela coroutines de debounce pendentes
            // tambem em OnDestroy (cobre destruicao sem passar por LeaveLobby/ClearLobbyState).
            foreach (var kvp in _memberAttributeDebounceCoroutines)
            {
                if (kvp.Value != null) StopCoroutine(kvp.Value);
            }
            _memberAttributeDebounceCoroutines.Clear();
            _pendingMemberAttributes.Clear();
        }

#if !EOS_DISABLE
        private LobbyInterface GetLobbyInterface()
        {
            return PlayEveryWare.EpicOnlineServices.EOSManager.Instance
                ?.GetEOSPlatformInterface()
                ?.GetLobbyInterface();
        }

        internal ProductUserId GetLocalUserId()
        {
            // TRAVA DE SEGURANÇA: Se o ID estiver vazio, retorna nulo para não quebrar a SDK
            string userIdStr = SessionManager.Instance?.GetUserId();
            if (string.IsNullOrEmpty(userIdStr)) return null;

            return ProductUserId.FromString(userIdStr);
        }
#endif

        public bool CreateLobby(LobbySettings settings)
        {
            return CreateLobbyEos(settings);
        }

        private bool CreateLobbyEos(LobbySettings settings)
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) { OnError?.Invoke("EOS nao inicializado"); return false; }

            var localUserId = GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                OnError?.Invoke("Usuario nao autenticado. Faca login antes de criar um lobby.");
                return false;
            }

            var options = new CreateLobbyOptions
            {
                LocalUserId = localUserId,
                MaxLobbyMembers = (uint)Mathf.Clamp(settings.maxPlayers, 2, 4),
                PermissionLevel = settings.isPublic
                                         ? LobbyPermissionLevel.Publicadvertised
                                         : LobbyPermissionLevel.Inviteonly,
                BucketId = BUCKET_ID,
                AllowInvites = true,
                PresenceEnabled = false,
                EnableJoinById = true,
                DisableHostMigration = true,
            };

            Debug.Log($"[LobbyManager] Criando lobby EOS: '{settings.lobbyName}'...");

            lobbyInterface.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo info) =>
            {
                if (info.ResultCode != Result.Success)
                {
                    Debug.LogError($"[LobbyManager] Erro ao criar lobby: {info.ResultCode}");
                    OnError?.Invoke($"Erro ao criar lobby: {info.ResultCode}");
                    return;
                }

                string lobbyId = info.LobbyId;
                Debug.Log($"[LobbyManager] Lobby criado no EOS: {lobbyId}");

                // C5 audit: popular _currentLobby ANTES de _isInLobby=true para
                // eliminar a janela de inconsistencia em que o flag indica "no lobby"
                // mas CurrentLobby ainda e null. Todos os dados necessarios sao sincronos.
                _currentLobby = new LobbyInfo
                {
                    lobbyId = lobbyId,
                    lobbyName = settings.lobbyName,
                    hostDisplayName = SessionManager.Instance.GetDisplayName(),
                    hostProductUserId = SessionManager.Instance.GetUserId(),
                    currentPlayers = 1,
                    maxPlayers = settings.maxPlayers,
                    mapName = settings.mapName,
                    isPublic = settings.isPublic,
                    state = LobbyState.WaitingForPlayers,
                };
                _isInLobby = true;

                SetLobbyAttributes(lobbyId, settings, () =>
                {
                    _membershipService.Clear();
                    _membershipService.AddMember(new LobbyMember(
                        SessionManager.Instance.GetUserId(),
                        SessionManager.Instance.GetDisplayName(),
                        host: true));

                    SessionManager.Instance.SetCurrentLobby(lobbyId);
                    OnLobbyCreated?.Invoke(_currentLobby);

                    // OPTIMIZATION (Sprint 4 / Item A6): inicializacao usa variante imediata (sem debounce).
                    SetMemberAttributeImmediate(MemberAttributes.DISPLAY_NAME,
                                                SessionManager.Instance.GetDisplayName());
                });
            });
            return true;
#else
            Debug.LogWarning("[LobbyManager] EOS desabilitado (EOS_DISABLE)");
            return false;
#endif
        }

        private void SetLobbyAttributes(string lobbyId, LobbySettings settings, Action onComplete)
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            var localUserId = GetLocalUserId();

            var modOptions = new UpdateLobbyModificationOptions
            {
                LocalUserId = localUserId,
                LobbyId = lobbyId,
            };

            if (lobbyInterface.UpdateLobbyModification(ref modOptions, out var mod) != Result.Success)
            {
                Debug.LogError("[LobbyManager] Falha ao obter LobbyModification");
                onComplete?.Invoke();
                return;
            }

            // A1 audit: se uma exception ocorrer entre aqui e a chamada UpdateLobby,
            // o handle 'mod' vazaria. O flag 'scheduled' marca quando UpdateLobby
            // aceitou o handle — apartir desse ponto o callback e responsavel pelo Release.
            bool scheduled = false;
            try
            {
                EosLobbyModHelper.AddStringAttr(mod, LobbyAttributes.LOBBY_NAME, settings.lobbyName, LobbyAttributeVisibility.Public);
                EosLobbyModHelper.AddStringAttr(mod, LobbyAttributes.MAP_NAME, settings.mapName, LobbyAttributeVisibility.Public);
                EosLobbyModHelper.AddInt64Attr(mod, LobbyAttributes.MAX_PLAYERS, settings.maxPlayers, LobbyAttributeVisibility.Public);
                EosLobbyModHelper.AddStringAttr(mod, LobbyAttributes.LOBBY_STATE, LobbyState.WaitingForPlayers.ToString(), LobbyAttributeVisibility.Public);
                // Campos reservados para StartMatch — clientes observam RELAY_CODE e SERVER_ADDRESS
                EosLobbyModHelper.AddStringAttr(mod, LobbyAttributes.SERVER_ADDRESS, "", LobbyAttributeVisibility.Public);
                EosLobbyModHelper.AddInt64Attr(mod, LobbyAttributes.SERVER_PORT, DEFAULT_PORT, LobbyAttributeVisibility.Public);
                EosLobbyModHelper.AddStringAttr(mod, LobbyAttributes.RELAY_CODE, "", LobbyAttributeVisibility.Public);

                var updateOpts = new UpdateLobbyOptions { LobbyModificationHandle = mod };
                lobbyInterface.UpdateLobby(ref updateOpts, null, (ref UpdateLobbyCallbackInfo info) =>
                {
                    mod.Release();
                    if (info.ResultCode != Result.Success)
                        Debug.LogWarning($"[LobbyManager] Atributos do lobby com erro: {info.ResultCode}");
                    onComplete?.Invoke();
                });
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                {
                    mod.Release();
                    onComplete?.Invoke();
                }
            }
#else
            onComplete?.Invoke();
#endif
        }

        public void SearchLobbies(LobbySearchFilter filter)
        {
            SearchLobbiesEos(filter);
        }

        private void SearchLobbiesEos(LobbySearchFilter filter)
        {
#if !EOS_DISABLE
            // OPTIMIZATION (Sprint 3 / Item A3): rate-limit + cache.
            // Se ja temos resultado recente, republica do cache sem disparar nova request.
            // Mantemos copia propria porque algumas UIs guardam a lista recebida e chamam
            // Clear() antes de buscar de novo; expor a mesma instancia limparia o cache.
            float elapsed = Time.unscaledTime - _lastSearchLobbiesTime;
            if (elapsed < SEARCH_LOBBIES_COOLDOWN_SECONDS && _lastSearchLobbiesResult != null)
            {
                Debug.Log($"[LobbyManager] SearchLobbies em cooldown ({elapsed:F1}s/{SEARCH_LOBBIES_COOLDOWN_SECONDS}s). " +
                          $"Republicando cache com {_lastSearchLobbiesResult.Count} lobbies.");
                OnLobbiesFound?.Invoke(new List<LobbyInfo>(_lastSearchLobbiesResult));
                return;
            }

            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) { OnError?.Invoke("EOS nao inicializado"); return; }

            var localUserId = GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                OnError?.Invoke("Usuario nao autenticado. Faca login antes de buscar lobbies.");
                return;
            }

            var createOpts = new CreateLobbySearchOptions { MaxResults = (uint)filter.maxResults };
            if (lobbyInterface.CreateLobbySearch(ref createOpts, out var searchHandle) != Result.Success || searchHandle == null)
            {
                OnError?.Invoke("Falha ao iniciar busca de lobbies");
                return;
            }

            if (!string.IsNullOrEmpty(filter.lobbyName))
            {
                var param = new LobbySearchSetParameterOptions
                {
                    Parameter = new AttributeData
                    {
                        Key = LobbyAttributes.LOBBY_NAME,
                        Value = new AttributeDataValue { AsUtf8 = filter.lobbyName },
                    },
                    ComparisonOp = ComparisonOp.Contains,
                };
                searchHandle.SetParameter(ref param);
            }

            var findOpts = new LobbySearchFindOptions { LocalUserId = localUserId };
            searchHandle.Find(ref findOpts, null, (ref LobbySearchFindCallbackInfo info) =>
            {
                if (info.ResultCode != Result.Success && info.ResultCode != Result.NotFound)
                {
                    searchHandle.Release();
                    Debug.LogError($"[LobbyManager] Erro na busca: {info.ResultCode}");
                    OnLobbiesFound?.Invoke(new List<LobbyInfo>());
                    return;
                }

                ReleaseDetailCache();

                var countOpts = new LobbySearchGetSearchResultCountOptions();
                uint count = searchHandle.GetSearchResultCount(ref countOpts);
                var results = new List<LobbyInfo>();

                for (uint i = 0; i < count; i++)
                {
                    var copyOpts = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = i };
                    if (searchHandle.CopySearchResultByIndex(ref copyOpts, out var details) != Result.Success)
                        continue;

                    // A4 audit: try/catch libera o handle se algo entre CopyInfo e
                    // _detailsCache[key] = details lancar. Em sucesso, details e transferido
                    // para _detailsCache (nao soltar) e sera liberado depois por ReleaseDetailCache.
                    bool transferred = false;
                    try
                    {
                        var infoOpts = new LobbyDetailsCopyInfoOptions();
                        if (details.CopyInfo(ref infoOpts, out var di) != Result.Success || !di.HasValue)
                            continue;

                        string ownerUserId = di.Value.LobbyOwnerUserId?.ToString() ?? "";
                        var lobby = new LobbyInfo
                        {
                            lobbyId = di.Value.LobbyId,
                            hostDisplayName = ownerUserId,
                            hostProductUserId = ownerUserId,
                            maxPlayers = (int)di.Value.MaxMembers,
                            currentPlayers = (int)(di.Value.MaxMembers - di.Value.AvailableSlots),
                            isPublic = di.Value.PermissionLevel == LobbyPermissionLevel.Publicadvertised,
                        };

                        var attrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.LOBBY_NAME };
                        if (details.CopyAttributeByKey(ref attrOpts, out var nameAttr) == Result.Success && nameAttr.HasValue)
                            lobby.lobbyName = nameAttr.Value.Data?.Value.AsUtf8 ?? "Sala";

                        attrOpts.AttrKey = LobbyAttributes.MAP_NAME;
                        if (details.CopyAttributeByKey(ref attrOpts, out var mapAttr) == Result.Success && mapAttr.HasValue)
                            lobby.mapName = mapAttr.Value.Data?.Value.AsUtf8 ?? "";

                        results.Add(lobby);
                        _detailsCache[di.Value.LobbyId] = details;
                        transferred = true;
                    }
                    finally
                    {
                        if (!transferred)
                            details.Release();
                    }
                }

                searchHandle.Release();
                Debug.Log($"[LobbyManager] Busca concluida: {results.Count} lobbies encontrados");
                _lastSearchLobbiesResult = new List<LobbyInfo>(results);
                _lastSearchLobbiesTime = Time.unscaledTime;
                OnLobbiesFound?.Invoke(results);
            });
#else
            OnLobbiesFound?.Invoke(new List<LobbyInfo>());
#endif
        }

        /// <summary>
        /// Invalida o cache de SearchLobbies. A proxima chamada dispara nova request
        /// mesmo dentro do cooldown; util quando um lobby acabou de ser criado/destruido.
        /// </summary>
        public void InvalidateLobbySearchCache()
        {
            _lastSearchLobbiesResult = null;
            _lastSearchLobbiesTime = -10f;
        }

        public void JoinLobby(string lobbyId)
        {
            JoinLobbyEos(lobbyId);
        }

        private void JoinLobbyEos(string lobbyId)
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) { OnError?.Invoke("EOS nao inicializado"); return; }

            // Se o handle nao esta em cache, busca por ID primeiro
            if (!_detailsCache.TryGetValue(lobbyId, out var details))
            {
                SearchByIdThenJoin(lobbyId);
                return;
            }

            var localUserId = GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                OnError?.Invoke("Usuario nao autenticado. Faca login antes de entrar em um lobby.");
                return;
            }

            var joinOpts = new JoinLobbyOptions
            {
                LobbyDetailsHandle = details,
                LocalUserId = localUserId,
                PresenceEnabled = false,
            };

            Debug.Log($"[LobbyManager] Entrando no lobby: {lobbyId}...");

            lobbyInterface.JoinLobby(ref joinOpts, null, (ref JoinLobbyCallbackInfo info) =>
            {
                if (info.ResultCode != Result.Success)
                {
                    Debug.LogError($"[LobbyManager] Erro ao entrar no lobby: {info.ResultCode}");
                    OnError?.Invoke($"Erro ao entrar: {info.ResultCode}");
                    return;
                }

                // C5 audit: placeholder sincrono de _currentLobby ANTES de _isInLobby=true.
                // PopulateLobbyInfoFromDetails e async e enriquece o objeto depois;
                // sem este placeholder, qualquer codigo observando IsInLobby encontraria null.
                _currentLobby = new LobbyInfo
                {
                    lobbyId = lobbyId,
                    hostProductUserId = "",
                    currentPlayers = 1,
                    state = LobbyState.WaitingForPlayers,
                };
                _isInLobby = true;
                SessionManager.Instance.SetCurrentLobby(lobbyId);

                PopulateLobbyInfoFromDetails(lobbyId, details, lobbyInfo =>
                {
                    _currentLobby = lobbyInfo;
                    _membershipService.Clear();

                    // O handle 'details' veio da busca e nao contem lista de membros.
                    // Apos o join, busca um handle fresco via CopyLobbyDetailsHandle.
                    var freshOpts = new CopyLobbyDetailsHandleOptions
                    {
                        LobbyId = lobbyId,
                        LocalUserId = GetLocalUserId(),
                    };
                    if (GetLobbyInterface()?.CopyLobbyDetailsHandle(ref freshOpts, out var freshDetails) == Result.Success)
                    {
                        _membershipService.PopulateMembersFromDetails(freshDetails, lobbyInfo.hostProductUserId);
                        // [SYNC-FIX] Verificar atributos imediatamente após o join (proativo)
                        _dispatcher?.ProcessLobbyAttributes(freshDetails);
                        freshDetails.Release();
                    }
                    else
                    {
                        Debug.LogWarning("[LobbyManager] CopyLobbyDetailsHandle falhou pos-join, adicionando jogador local manualmente");
                        _membershipService.AddMember(new LobbyMember(
                            SessionManager.Instance.GetUserId(),
                            SessionManager.Instance.GetDisplayName()));
                    }

                    OnLobbyJoined?.Invoke(_currentLobby);
                    Debug.Log($"[LobbyManager] Entrou com sucesso: {lobbyId}");

                    // Publicar nome de exibicao como atributo de membro
                    // para que o host e outros membros possam ler via CopyMemberAttributeByKey
                    // OPTIMIZATION (Sprint 4 / Item A6): inicializacao usa variante imediata (sem debounce).
                    SetMemberAttributeImmediate(MemberAttributes.DISPLAY_NAME,
                                                SessionManager.Instance.GetDisplayName());
                });
            });
#endif
        }

        private void SearchByIdThenJoin(string lobbyId)
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            var localUserId = GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                OnError?.Invoke("Usuario nao autenticado. Faca login antes de entrar em um lobby.");
                return;
            }

            var createOpts = new CreateLobbySearchOptions { MaxResults = 1 };
            if (lobbyInterface.CreateLobbySearch(ref createOpts, out var searchHandle) != Result.Success || searchHandle == null)
            {
                OnError?.Invoke("Falha ao buscar lobby por ID");
                return;
            }

            var setIdOpts = new LobbySearchSetLobbyIdOptions { LobbyId = lobbyId };
            searchHandle.SetLobbyId(ref setIdOpts);

            var findOpts = new LobbySearchFindOptions { LocalUserId = localUserId };
            searchHandle.Find(ref findOpts, null, (ref LobbySearchFindCallbackInfo info) =>
            {
                var countOpts = new LobbySearchGetSearchResultCountOptions();
                if (info.ResultCode != Result.Success || searchHandle.GetSearchResultCount(ref countOpts) == 0)
                {
                    searchHandle.Release();
                    OnError?.Invoke($"Lobby '{lobbyId}' nao encontrado");
                    return;
                }

                var copyOpts = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = 0 };
                if (searchHandle.CopySearchResultByIndex(ref copyOpts, out var details) == Result.Success)
                {
                    searchHandle.Release();
                    _detailsCache[lobbyId] = details;
                    JoinLobby(lobbyId);
                }
                else
                {
                    searchHandle.Release();
                    OnError?.Invoke("Falha ao obter detalhes do lobby por ID");
                }
            });
#endif
        }

        public void LeaveLobby()
        {
            if (!_isInLobby) { Debug.LogWarning("[LobbyManager] Nao esta em um lobby"); return; }

            LeaveLobbyEos();
        }

        private void LeaveLobbyEos()
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) { OnError?.Invoke("EOS nao inicializado"); return; }

            var localUserId = GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                ClearLobbyState();
                OnLobbyLeft?.Invoke();
                return;
            }

            var options = new LeaveLobbyOptions
            {
                LocalUserId = localUserId,
                LobbyId = _currentLobby?.lobbyId ?? "",
            };

            Debug.Log($"[LobbyManager] Saindo do lobby: {_currentLobby?.lobbyId}...");

            lobbyInterface.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) =>
            {
                if (info.ResultCode != Result.Success)
                    Debug.LogWarning($"[LobbyManager] Erro ao sair do lobby: {info.ResultCode}");

                ClearLobbyState();
                OnLobbyLeft?.Invoke();
                Debug.Log("[LobbyManager] Saiu do lobby");
            });
#else
            ClearLobbyState();
            OnLobbyLeft?.Invoke();
#endif
        }

        /// <summary>
        /// Limpa o estado de lobby local de forma síncrona e dispara OnLobbyLeft imediatamente.
        /// Envia o pedido de saída ao EOS em background (fire-and-forget — sem callback de estado).
        /// Use este método antes de transições de cena para garantir estado limpo na cena destino.
        /// </summary>
        public void ForceLeaveImmediate()
        {
            if (!_isInLobby) return;

#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            var localUserId    = GetLocalUserId();
            string lobbyId     = _currentLobby?.lobbyId ?? "";

            if (lobbyInterface != null && localUserId != null && localUserId.IsValid()
                && !string.IsNullOrEmpty(lobbyId))
            {
                var options = new LeaveLobbyOptions { LocalUserId = localUserId, LobbyId = lobbyId };
                lobbyInterface.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) =>
                {
                    if (info.ResultCode != Result.Success)
                        Debug.LogWarning($"[LobbyManager] ForceLeaveImmediate EOS callback: {info.ResultCode}");
                });
            }
#endif

            ClearLobbyState();
            OnLobbyLeft?.Invoke();
        }

        public void CancelPendingClientConnect()
        {
            if (ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.HasInstance)
            {
                ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.Instance.CancelPendingConnect();
            }
        }

        public void ForceResetRuntimeState(bool notifyLobbyLeft = false)
        {
            CancelPendingClientConnect();
            ClearLobbyState();

            if (notifyLobbyLeft)
                OnLobbyLeft?.Invoke();
        }

        /// <summary>
        /// API publica - debounce de 250ms. Chamadas rapidas para a mesma key colapsam em uma.
        /// Para chamada imediata (inicializacao do lobby), use SetMemberAttributeImmediate.
        /// Ex: IS_READY=True, CHARACTER_INDEX=2
        /// </summary>
        public void SetMemberAttribute(string key, string value)
        {
            // OPTIMIZATION (Sprint 4 / Item A6 - 2026-05-21): debounce. Ver comentario nos campos privados.
            if (!_isInLobby || _currentLobby == null) return;

#if UNITY_EDITOR
            Debug.Log($"[LobbyManager] SetMemberAttribute (debounced) key={key} value={value}");
#endif

            _pendingMemberAttributes[key] = value;

            if (_memberAttributeDebounceCoroutines.TryGetValue(key, out var existing) && existing != null)
                StopCoroutine(existing);

            _memberAttributeDebounceCoroutines[key] = StartCoroutine(DebouncedSubmitMemberAttribute(key));
        }

        private System.Collections.IEnumerator DebouncedSubmitMemberAttribute(string key)
        {
            yield return new WaitForSeconds(SET_MEMBER_ATTRIBUTE_DEBOUNCE_SECONDS);

            string pendingValue = null;
            bool hasPending = _pendingMemberAttributes.TryGetValue(key, out pendingValue);
            _pendingMemberAttributes.Remove(key);
            _memberAttributeDebounceCoroutines.Remove(key);

            if (hasPending)
                SetMemberAttributeImmediate(key, pendingValue);
        }

        /// <summary>
        /// Variante imediata. Usada internamente pela coroutine de debounce + chamadas
        /// de inicializacao (CreateLobby, JoinLobby) que NAO devem ser debounced.
        /// </summary>
        private void SetMemberAttributeImmediate(string key, string value)
        {
            if (!_isInLobby || _currentLobby == null) return;

#if UNITY_EDITOR
            Debug.Log($"[LobbyManager] SetMemberAttributeImmediate -> EOS call: key={key} value={value}");
#endif

            SetMemberAttributeEos(key, value);
        }

        private void SetMemberAttributeEos(string key, string value)
        {
#if !EOS_DISABLE
            // A5 audit: todos os caminhos de falha agora propagam OnError para UI,
            // em vez de so logar silenciosamente. Antes, o toggle Ready / selecao de
            // personagem podia falhar no backend sem que o usuario percebesse.
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null)
            {
                Debug.LogWarning($"[LobbyManager] SetMemberAttribute('{key}') abortado: EOS nao inicializado");
                OnError?.Invoke($"Nao foi possivel sincronizar '{key}': EOS nao inicializado");
                return;
            }

            var localUserId = GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                Debug.LogWarning("[LobbyManager] SetMemberAttribute chamado sem autenticacao");
                OnError?.Invoke($"Nao foi possivel sincronizar '{key}': usuario nao autenticado");
                return;
            }

            var modOpts = new UpdateLobbyModificationOptions
            {
                LocalUserId = localUserId,
                LobbyId = _currentLobby.lobbyId,
            };

            if (lobbyInterface.UpdateLobbyModification(ref modOpts, out var mod) != Result.Success)
            {
                Debug.LogError("[LobbyManager] Falha ao obter LobbyModification para atributo de membro");
                OnError?.Invoke($"Falha ao preparar atualizacao de '{key}'");
                return;
            }

            // A1 audit: guard anti-leak entre UpdateLobbyModification e UpdateLobby.
            bool scheduled = false;
            try
            {
                EosLobbyModHelper.AddStringMemberAttr(mod, key, value, LobbyAttributeVisibility.Public);

                var updateOpts = new UpdateLobbyOptions { LobbyModificationHandle = mod };
                lobbyInterface.UpdateLobby(ref updateOpts, null, (ref UpdateLobbyCallbackInfo info) =>
                {
                    mod.Release();
                    if (info.ResultCode == Result.Success)
                    {
                        Debug.Log($"[LobbyManager] Atributo definido: {key} = {value}");
                    }
                    else
                    {
                        Debug.LogWarning($"[LobbyManager] Falha ao definir '{key}': {info.ResultCode}");
                        OnError?.Invoke($"Falha ao sincronizar '{key}': {info.ResultCode}");
                    }
                });
                scheduled = true;
            }
            finally
            {
                if (!scheduled) mod.Release();
            }
#else
            Debug.LogWarning($"[LobbyManager] SetMemberAttribute('{key}') ignorado: EOS desabilitado");
            OnError?.Invoke($"Nao foi possivel sincronizar '{key}': EOS desabilitado");
#endif
        }

        public void SetReady(bool ready)
        {
            string myUid = SessionManager.Instance?.GetUserId();
            if (!string.IsNullOrEmpty(myUid))
            {
                var me = _membershipService.FindMutableMember(myUid);
                if (me != null)
                {
                    me.isReady = ready;
                    OnMemberUpdated?.Invoke(me);
                }
            }
            SetMemberAttribute(MemberAttributes.IS_READY, ready.ToString());
        }

        public void SelectCharacter(int characterIndex)
        {
            // Optimistic update: aplica no _members local ANTES do roundtrip EOS para que
            // GetMyCharacterIndex() — usado no payload de ConnectionApproval durante StartMatch —
            // nunca leia -1 quando o usuario clica "Iniciar" logo apos escolher o personagem.
            // O EOS vai confirmar via OnMemberAttributeChanged; se falhar, o estado real do servidor
            // sobrescrevera o valor local na proxima notificacao.
            string myUid = SessionManager.Instance?.GetUserId();
            if (!string.IsNullOrEmpty(myUid))
            {
                var me = _membershipService.FindMutableMember(myUid);
                if (me != null)
                {
                    me.selectedCharacterIndex = characterIndex;
                    Debug.Log($"[LobbyManager] SelectCharacter local: {characterIndex} (uid={myUid})");
                    OnMemberUpdated?.Invoke(me);
                }
            }

            SetMemberAttribute(MemberAttributes.CHARACTER_INDEX, characterIndex.ToString());
        }

        /// <summary>
        /// Retorna o indice do personagem escolhido pelo jogador local,
        /// lendo do proprio slot em _members. Fallback: 0.
        /// </summary>
        internal int GetMyCharacterIndex()
        {
            string myUid = SessionManager.Instance?.GetUserId();
            if (!string.IsNullOrEmpty(myUid) &&
                GameDataManager.Instance != null &&
                GameDataManager.Instance.equipeSelecionada != null)
            {
                int myIndex = GetCanonicalMemberIndex(myUid);
                int totalPlayers = GetOrderedMembers().Count;
                if (myIndex >= 0)
                {
                    int commanderSlot = PartySlotLayout.GetCommanderSlot(totalPlayers, myIndex);
                    CharacterBase[] equipe = GameDataManager.Instance.equipeSelecionada;
                    if (commanderSlot >= 0 && commanderSlot < equipe.Length)
                    {
                        CharacterBase charBase = equipe[commanderSlot];
                        if (charBase != null && GameDataManager.Instance.bibliotecaOriginalPersonagens != null)
                        {
                            string cleanName = charBase.name.Replace("(Clone)", "");
                            int index = GameDataManager.Instance.bibliotecaOriginalPersonagens.FindIndex(
                                c => c != null && c.name == cleanName);
                            if (index >= 0)
                                return index;
                        }
                    }
                }
            }

            // Fallback
            if (string.IsNullOrEmpty(myUid)) return 0;
            var me = GetOrderedMembers().Find(m => m.productUserId == myUid);
            return me != null && me.selectedCharacterIndex >= 0 ? me.selectedCharacterIndex : 0;
        }



        /// <summary>
        /// Inicia a partida como Host:
        ///   1. Inicia NGO Host
        ///   2. Publica SERVER_ADDRESS + SERVER_PORT como atributos do lobby
        ///   3. Carrega a cena de jogo via NGO SceneManager (todos os clientes seguem)
        ///
        /// Clientes recebem OnLobbyAttributeUpdated, leem SERVER_ADDRESS e chamam StartClient.
        /// </summary>
        public void StartMatch(string mapOverride = null)
        {
            if (!_isInLobby || _currentLobby == null)
            {
                Debug.LogWarning("[LobbyManager] StartMatch chamado fora de um lobby");
                return;
            }

            Debug.Log("[LobbyManager] Iniciando partida como HOST...");

            // Segurança: apenas o host do lobby EOS pode disparar StartMatch.
            string myUid = SessionManager.Instance?.GetUserId() ?? "";
            if (string.IsNullOrEmpty(myUid))
            {
                const string message = "Usuario nao autenticado. Faca login antes de iniciar a partida.";
                Debug.LogError("[LobbyManager] StartMatch abortado: sessao local sem ProductUserId");
                OnError?.Invoke(message);
                return;
            }

            if (_currentLobby != null && _currentLobby.hostProductUserId != myUid)
            {
                Debug.LogError($"[LobbyManager] Abortado: Player local ({myUid}) nao e o host da sala ({_currentLobby.hostProductUserId})");
                OnError?.Invoke("Apenas o host da sala pode iniciar a partida.");
                return;
            }

            int computedHostCharIndex = GetMyCharacterIndex();

            ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.Instance.LaunchAsHost(
                mapOverride,
                _currentLobby,
                computedHostCharIndex,
                (err) => OnError?.Invoke(err)
            );
        }


        private static void PopulateLobbyInfoFromDetails(string lobbyId, LobbyDetails details, Action<LobbyInfo> onResult)
        {
            var result = new LobbyInfo { lobbyId = lobbyId };

            var infoOpts = new LobbyDetailsCopyInfoOptions();
            if (details.CopyInfo(ref infoOpts, out var di) == Result.Success && di.HasValue)
            {
                result.maxPlayers = (int)di.Value.MaxMembers;
                result.currentPlayers = (int)(di.Value.MaxMembers - di.Value.AvailableSlots);
                result.isPublic = di.Value.PermissionLevel == LobbyPermissionLevel.Publicadvertised;
                result.hostProductUserId = di.Value.LobbyOwnerUserId?.ToString() ?? "";
            }

            var attrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.LOBBY_NAME };
            if (details.CopyAttributeByKey(ref attrOpts, out var nameAttr) == Result.Success && nameAttr.HasValue)
                result.lobbyName = nameAttr.Value.Data?.Value.AsUtf8 ?? "Sala";

            attrOpts.AttrKey = LobbyAttributes.MAP_NAME;
            if (details.CopyAttributeByKey(ref attrOpts, out var mapAttr) == Result.Success && mapAttr.HasValue)
                result.mapName = mapAttr.Value.Data?.Value.AsUtf8 ?? "";

            attrOpts.AttrKey = LobbyAttributes.LOBBY_STATE;
            if (details.CopyAttributeByKey(ref attrOpts, out var stateAttr) == Result.Success && stateAttr.HasValue)
            {
                string stateStr = stateAttr.Value.Data?.Value.AsUtf8 ?? "";
                if (System.Enum.TryParse(stateStr, out LobbyState parsedState))
                    result.state = parsedState;
            }

            onResult?.Invoke(result);
        }


        internal void ClearLobbyState()
        {
            CancelPendingClientConnect();
            // OPTIMIZATION (Sprint 4 / Item A6): cancela coroutines de debounce pendentes ao
            // sair/limpar o lobby. Evita que uma chamada agendada dispare apos _currentLobby == null.
            foreach (var kvp in _memberAttributeDebounceCoroutines)
            {
                if (kvp.Value != null) StopCoroutine(kvp.Value);
            }
            _memberAttributeDebounceCoroutines.Clear();
            _pendingMemberAttributes.Clear();

            _isInLobby = false;
            _currentLobby = null;
            _membershipService.Clear();

            var session = SessionManager.TryGetExistingInstance();
            if (session != null)
            {
                session.SetCurrentLobby("");
                session.SetCurrentMatch("");
            }

            ReleaseDetailCache();
            // Ao sair de um lobby, descarta escolhas de personagem que ainda estariam cacheadas
            // de uma tentativa de StartMatch anterior (ex: host saiu mid-match ou partida foi abortada).
            CharacterChoiceCache.Clear();
        }

        private void ReleaseDetailCache()
        {
#if !EOS_DISABLE
            foreach (var d in _detailsCache.Values)
                d.Release();
#endif
            _detailsCache.Clear();
        }

        public static string GetLocalIpAddress()
        {
            return NetworkAddressHelper.GetLocalIpAddress();
        }

        public bool IsInLobby() => _isInLobby;
        public LobbyInfo GetCurrentLobby() => _currentLobby;
        public List<LobbyMember> GetMembers() => _membershipService.GetMembers();

        internal LobbyMember FindMutableMember(string productUserId)
        {
            return _membershipService.FindMutableMember(productUserId);
        }

        internal bool TryAddMemberFromNotification(LobbyMember member)
        {
            return _membershipService.TryAddMemberFromNotification(member);
        }

        internal LobbyMember TryRemoveMemberFromNotification(string productUserId)
        {
            return _membershipService.TryRemoveMemberFromNotification(productUserId);
        }

        internal void RefreshCurrentPlayerCountFromMembers()
        {
            _membershipService.RefreshCurrentPlayerCountFromMembers();
        }

        public List<LobbyMember> GetOrderedMembers() => _membershipService.GetOrderedMembers();
        public int GetCanonicalMemberIndex(string productUserId) => _membershipService.GetCanonicalMemberIndex(productUserId);
    }
}
