using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
        private List<LobbyMember> _members = new List<LobbyMember>();

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

        private ulong _memberStatusHandle;
        private ulong _lobbyUpdateHandle;
        private ulong _memberUpdateHandle;

        // Cache do EOSManagerWrapper para evitar lazy-create em OnDestroy
        private Core.EOSManagerWrapper _eosCache;

        // Coroutine de conexao cliente em andamento — cancelada se StartMatch for chamado no host


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
            if (_eosCache.IsInitialized)
                RegisterNotifications();
            else
                _eosCache.OnEOSInitialized += RegisterNotifications;
        }

        private void OnDestroy()
        {
            if (_eosCache != null)
                _eosCache.OnEOSInitialized -= RegisterNotifications;
            UnregisterNotifications();
            ReleaseDetailCache();
        }

#if !EOS_DISABLE
        private LobbyInterface GetLobbyInterface()
        {
            return PlayEveryWare.EpicOnlineServices.EOSManager.Instance
                ?.GetEOSPlatformInterface()
                ?.GetLobbyInterface();
        }

        private ProductUserId GetLocalUserId()
        {
            // TRAVA DE SEGURANÇA: Se o ID estiver vazio, retorna nulo para não quebrar a SDK
            string userIdStr = SessionManager.Instance?.GetUserId();
            if (string.IsNullOrEmpty(userIdStr)) return null;

            return ProductUserId.FromString(userIdStr);
        }
#endif

        public bool CreateLobby(LobbySettings settings)
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
                    _members.Clear();
                    _members.Add(new LobbyMember(
                        SessionManager.Instance.GetUserId(),
                        SessionManager.Instance.GetDisplayName(),
                        host: true));

                    SessionManager.Instance.SetCurrentLobby(lobbyId);
                    OnLobbyCreated?.Invoke(_currentLobby);

                    SetMemberAttribute(MemberAttributes.DISPLAY_NAME,
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
                AddStringAttr(mod, LobbyAttributes.LOBBY_NAME, settings.lobbyName, LobbyAttributeVisibility.Public);
                AddStringAttr(mod, LobbyAttributes.MAP_NAME, settings.mapName, LobbyAttributeVisibility.Public);
                AddInt64Attr(mod, LobbyAttributes.MAX_PLAYERS, settings.maxPlayers, LobbyAttributeVisibility.Public);
                AddStringAttr(mod, LobbyAttributes.LOBBY_STATE, LobbyState.WaitingForPlayers.ToString(), LobbyAttributeVisibility.Public);
                // Campos reservados para StartMatch — clientes observam RELAY_CODE e SERVER_ADDRESS
                AddStringAttr(mod, LobbyAttributes.SERVER_ADDRESS, "", LobbyAttributeVisibility.Public);
                AddInt64Attr(mod, LobbyAttributes.SERVER_PORT, DEFAULT_PORT, LobbyAttributeVisibility.Public);
                AddStringAttr(mod, LobbyAttributes.RELAY_CODE, "", LobbyAttributeVisibility.Public);

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
                    _members.Clear();

                    // O handle 'details' veio da busca e nao contem lista de membros.
                    // Apos o join, busca um handle fresco via CopyLobbyDetailsHandle.
                    var freshOpts = new CopyLobbyDetailsHandleOptions
                    {
                        LobbyId = lobbyId,
                        LocalUserId = GetLocalUserId(),
                    };
                    if (GetLobbyInterface()?.CopyLobbyDetailsHandle(ref freshOpts, out var freshDetails) == Result.Success)
                    {
                        PopulateMembersFromDetails(freshDetails, lobbyInfo.hostProductUserId);
                        // [SYNC-FIX] Verificar atributos imediatamente após o join (proativo)
                        ProcessLobbyAttributes(freshDetails);
                        freshDetails.Release();
                    }
                    else
                    {
                        Debug.LogWarning("[LobbyManager] CopyLobbyDetailsHandle falhou pos-join, adicionando jogador local manualmente");
                        _members.Add(new LobbyMember(
                            SessionManager.Instance.GetUserId(),
                            SessionManager.Instance.GetDisplayName()));
                    }

                    OnLobbyJoined?.Invoke(_currentLobby);
                    Debug.Log($"[LobbyManager] Entrou com sucesso: {lobbyId}");

                    // Publicar nome de exibicao como atributo de membro
                    // para que o host e outros membros possam ler via CopyMemberAttributeByKey
                    SetMemberAttribute(MemberAttributes.DISPLAY_NAME,
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
        /// Define um atributo do jogador local no lobby atual.
        /// Ex: IS_READY=True, CHARACTER_INDEX=2
        /// </summary>
        public void SetMemberAttribute(string key, string value)
        {
            if (!_isInLobby || _currentLobby == null) return;

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
                AddStringMemberAttr(mod, key, value, LobbyAttributeVisibility.Public);

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
#endif
        }

        public void SetReady(bool ready)
        {
            string myUid = SessionManager.Instance?.GetUserId();
            if (!string.IsNullOrEmpty(myUid))
            {
                var me = _members.Find(m => m.productUserId == myUid);
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
                var me = _members.Find(m => m.productUserId == myUid);
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
        private int GetMyCharacterIndex()
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
            if (!string.IsNullOrEmpty(myUid) && _currentLobby != null && _currentLobby.hostProductUserId != myUid)
            {
                Debug.LogError($"[LobbyManager] Abortado: Player local ({myUid}) nao e o host da sala ({_currentLobby.hostProductUserId})");
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


        private void RegisterNotifications()
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) return;

            var memberOpts = new AddNotifyLobbyMemberStatusReceivedOptions();
            _memberStatusHandle = lobbyInterface.AddNotifyLobbyMemberStatusReceived(
                ref memberOpts, null, OnMemberStatusChanged);

            var updateOpts = new AddNotifyLobbyUpdateReceivedOptions();
            _lobbyUpdateHandle = lobbyInterface.AddNotifyLobbyUpdateReceived(
                ref updateOpts, null, OnLobbyAttributeUpdated);

            var memberUpdateOpts = new AddNotifyLobbyMemberUpdateReceivedOptions();
            _memberUpdateHandle = lobbyInterface.AddNotifyLobbyMemberUpdateReceived(
                ref memberUpdateOpts, null, OnMemberAttributeChanged);

            Debug.Log("[LobbyManager] Notificacoes EOS registradas");
#endif
        }

        private void UnregisterNotifications()
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) return;

            if (_memberStatusHandle != 0)
                lobbyInterface.RemoveNotifyLobbyMemberStatusReceived(_memberStatusHandle);
            if (_lobbyUpdateHandle != 0)
                lobbyInterface.RemoveNotifyLobbyUpdateReceived(_lobbyUpdateHandle);
            if (_memberUpdateHandle != 0)
                lobbyInterface.RemoveNotifyLobbyMemberUpdateReceived(_memberUpdateHandle);
#endif
        }

#if !EOS_DISABLE
        private void OnMemberStatusChanged(ref LobbyMemberStatusReceivedCallbackInfo info)
        {
            if (!_isInLobby || _currentLobby == null) return;
            if (info.LobbyId != _currentLobby.lobbyId) return;

            string userId = info.TargetUserId?.ToString() ?? "";

            switch (info.CurrentStatus)
            {
                case LobbyMemberStatus.Joined:
                    if (!_members.Exists(m => m.productUserId == userId))
                    {
                        // Tentar ler o DISPLAY_NAME do atributo de membro (definido pelo cliente ao entrar)
                        // Pode nao estar disponivel imediatamente — fallback para ID curto
                        string displayName = ReadMemberDisplayName(info.LobbyId, userId);
                        if (string.IsNullOrEmpty(displayName))
                            displayName = userId.Length > 8 ? $"Jogador_{userId.Substring(0, 8)}" : userId;

                        var member = new LobbyMember(userId, displayName);
                        _members.Add(member);
                        OnMemberJoined?.Invoke(member);
                    }
                    Debug.Log($"[LobbyManager] Membro entrou: {userId}");
                    break;

                case LobbyMemberStatus.Left:
                case LobbyMemberStatus.Disconnected:
                case LobbyMemberStatus.Kicked:
                    var leaving = _members.Find(m => m.productUserId == userId);
                    if (leaving != null)
                    {
                        _members.Remove(leaving);
                        OnMemberLeft?.Invoke(leaving);
                    }
                    Debug.Log($"[LobbyManager] Membro saiu ({info.CurrentStatus}): {userId}");
                    break;

                case LobbyMemberStatus.Closed:
                    Debug.Log("[LobbyManager] Lobby fechado pelo host");
                    ClearLobbyState();
                    OnLobbyLeft?.Invoke();
                    break;
            }
        }

        // Chamado quando atributos de UM MEMBRO mudam (ex: IS_READY, CHARACTER_INDEX)
        private void OnMemberAttributeChanged(ref LobbyMemberUpdateReceivedCallbackInfo info)
        {
            if (ExoBeasts.Managers.GameModeManager.CurrentMode != ExoBeasts.Managers.GameMode.Multiplayer)
            {
                CancelPendingClientConnect();
                return;
            }

            if (!_isInLobby || _currentLobby == null) return;
            if (info.LobbyId != _currentLobby.lobbyId) return;

            string userId = info.TargetUserId?.ToString() ?? "";
            var member = _members.Find(m => m.productUserId == userId);
            if (member == null) return;

            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) return;

            var detailsOpts = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = info.LobbyId,
                LocalUserId = GetLocalUserId(),
            };

            if (lobbyInterface.CopyLobbyDetailsHandle(ref detailsOpts, out var details) != Result.Success)
                return;

            bool oldReady = member.isReady;
            string oldDisplayName = member.displayName;

            // A1 audit: try/finally garante release mesmo se CopyMemberAttributeByKey
            // lancar exceptionalmente. Antes, o Release() podia ficar inalcancavel.
            try
            {
                var readyOpts = new LobbyDetailsCopyMemberAttributeByKeyOptions
                {
                    TargetUserId = info.TargetUserId,
                    AttrKey = MemberAttributes.IS_READY,
                };
                if (details.CopyMemberAttributeByKey(ref readyOpts, out var readyAttr) == Result.Success && readyAttr.HasValue)
                    bool.TryParse(readyAttr.Value.Data?.Value.AsUtf8, out member.isReady);

                // Atualizar displayName silenciosamente se ainda era um ID curto (fallback)
                var nameOpts = new LobbyDetailsCopyMemberAttributeByKeyOptions
                {
                    TargetUserId = info.TargetUserId,
                    AttrKey = MemberAttributes.DISPLAY_NAME,
                };
                if (details.CopyMemberAttributeByKey(ref nameOpts, out var nameAttr) == Result.Success && nameAttr.HasValue)
                {
                    string newName = nameAttr.Value.Data?.Value.AsUtf8 ?? "";
                    if (!string.IsNullOrEmpty(newName))
                        member.displayName = newName;
                }

                var charOpts = new LobbyDetailsCopyMemberAttributeByKeyOptions
                {
                    TargetUserId = info.TargetUserId,
                    AttrKey = MemberAttributes.CHARACTER_INDEX,
                };
                if (details.CopyMemberAttributeByKey(ref charOpts, out var charAttr) == Result.Success && charAttr.HasValue)
                {
                    string charVal = charAttr.Value.Data?.Value.AsUtf8 ?? "";
                    if (int.TryParse(charVal, out int charIdx))
                        member.selectedCharacterIndex = charIdx;
                }

                // [SYNC-FIX] Verificar se a partida já começou (proativo)
                ProcessLobbyAttributes(details);
            }
            finally
            {
                details.Release();
            }

            // Notifica UI quando isReady ou displayName muda.
            // displayName chega assíncrono (SetMemberAttribute após join) — deve re-renderizar.
            if (member.isReady != oldReady || member.displayName != oldDisplayName)
            {
                Debug.Log($"[LobbyManager] Membro atualizado: {userId} | isReady={member.isReady} | nome={member.displayName}");
                OnMemberUpdated?.Invoke(member);
            }
        }

        // Chamado quando atributos do lobby mudam (clientes detectam SERVER_ADDRESS aqui)
        private void OnLobbyAttributeUpdated(ref LobbyUpdateReceivedCallbackInfo info)
        {
            if (ExoBeasts.Managers.GameModeManager.CurrentMode != ExoBeasts.Managers.GameMode.Multiplayer)
            {
                CancelPendingClientConnect();
                return;
            }

            Debug.Log($"[LobbyManager][DBG] OnLobbyAttributeUpdated — LobbyId={info.LobbyId} | _isInLobby={_isInLobby} | currentLobby={_currentLobby?.lobbyId ?? "null"}");

            if (!_isInLobby || _currentLobby == null)
            {
                Debug.LogWarning("[LobbyManager][DBG] Ignorado: nao esta em lobby ou _currentLobby nulo");
                return;
            }
            if (info.LobbyId != _currentLobby.lobbyId)
            {
                Debug.LogWarning($"[LobbyManager][DBG] Ignorado: LobbyId nao corresponde ({info.LobbyId} != {_currentLobby.lobbyId})");
                return;
            }

            Debug.Log($"[LobbyManager] Notificacao de atributo recebida para Lobby {info.LobbyId}. Verificando estado da rede...");

            var lobbyInterface = GetLobbyInterface();
            var detailsOpts = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = info.LobbyId,
                LocalUserId = GetLocalUserId(),
            };

            var copyResult = lobbyInterface.CopyLobbyDetailsHandle(ref detailsOpts, out var details);
            if (copyResult != Result.Success)
            {
                Debug.LogError($"[LobbyManager][DBG] CopyLobbyDetailsHandle falhou: {copyResult}");
                return;
            }

            try
            {
                ProcessLobbyAttributes(details);
            }
            finally
            {
                details.Release();
            }
        }

        /// <summary>
        /// [SYNC-FIX] Extrai atributos de rede do lobby e inicia conexão se STATE=InGame.
        /// Centralizado para ser chamado por notificações e proativamente no Join.
        /// </summary>
        private void ProcessLobbyAttributes(LobbyDetails details)
        {
            if (details == null) return;

            if (ExoBeasts.Managers.GameModeManager.CurrentMode != ExoBeasts.Managers.GameMode.Multiplayer)
            {
                CancelPendingClientConnect();
                return;
            }

            // O host do lobby EOS nunca conecta como cliente NGO.
            string _myUid = SessionManager.Instance?.GetUserId() ?? "";
            if (!string.IsNullOrEmpty(_myUid) && _currentLobby != null && _currentLobby.hostProductUserId == _myUid)
            {
                return;
            }

            // Ja conectado como cliente ativo num jogo real — nao reconectar.
            // IsConnectedClient so vira true apos handshake completo com o servidor.
            // IsHost=true sozinho NAO bloqueia: pode ser um StartHost() do MenuScene que
            // precisa ser derrubado — ConnectClientCoroutine faz o Shutdown() antes de StartClient().
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsClient &&
                !NetworkManager.Singleton.IsHost &&
                NetworkManager.Singleton.IsConnectedClient)
            {
                return;
            }

            // Verificar LOBBY_STATE
            var stateAttrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.LOBBY_STATE };
            if (details.CopyAttributeByKey(ref stateAttrOpts, out var stateAttr) == Result.Success && stateAttr.HasValue)
            {
                string stateStr = stateAttr.Value.Data?.Value.AsUtf8 ?? "";
                if (stateStr != LobbyState.InGame.ToString() && stateStr != "Starting")
                {
                    // Se não está em InGame ou Starting, ignora (ainda esperando)
                    return;
                }
                Debug.Log($"[LobbyManager][DBG] Lobby em estado '{stateStr}' — processando dados de conexão...");
            }

            // Verificar RELAY_CODE primeiro
            var relayAttrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.RELAY_CODE };
            var relayAttrResult = details.CopyAttributeByKey(ref relayAttrOpts, out var relayAttr);
            if (relayAttrResult == Result.Success && relayAttr.HasValue)
            {
                string relayCode = relayAttr.Value.Data?.Value.AsUtf8 ?? "";
                if (ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.IsUsableRelayCode(relayCode))
                {
                    Debug.Log($"[LobbyManager] Conectando via Relay: {relayCode}");
                    int myChar = GetMyCharacterIndex();
                    ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.Instance.ConnectAsClientViaRelay(
                        relayCode, myChar, (err) => OnError?.Invoke(err));
                    return;
                }
                else if (!string.IsNullOrEmpty(relayCode))
                {
                    Debug.Log("[LobbyManager] RELAY_CODE sentinel/invalidado. Usando fallback SERVER_ADDRESS.");
                }
            }

            // Fallback: SERVER_ADDRESS
            var addrAttrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.SERVER_ADDRESS };
            var addrResult = details.CopyAttributeByKey(ref addrAttrOpts, out var addrAttr);
            if (addrResult == Result.Success && addrAttr.HasValue)
            {
                string serverAddress = addrAttr.Value.Data?.Value.AsUtf8 ?? "";
                if (!string.IsNullOrEmpty(serverAddress))
                {
                    ushort port = ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.DEFAULT_PORT;
                    addrAttrOpts.AttrKey = LobbyAttributes.SERVER_PORT;
                    if (details.CopyAttributeByKey(ref addrAttrOpts, out var portAttr) == Result.Success && portAttr.HasValue)
                        port = (ushort)(portAttr.Value.Data?.Value.AsInt64 ?? ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.DEFAULT_PORT);

                    Debug.Log($"[LobbyManager] Conectando via IP: {serverAddress}:{port}");
                    int myChar = GetMyCharacterIndex();
                    ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.Instance.ConnectAsClientViaIp(
                        serverAddress, port, myChar, (err) => OnError?.Invoke(err));
                }
            }
        }



#endif

#if !EOS_DISABLE
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

        private static void AddStringMemberAttr(LobbyModification mod, string key, string value, LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddMemberAttributeOptions
            {
                Attribute = new AttributeData { Key = key, Value = new AttributeDataValue { AsUtf8 = value } },
                Visibility = vis,
            };
            mod.AddMemberAttribute(ref opts);
        }

        private string ReadMemberDisplayName(string lobbyId, string userId)
        {
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) return "";

            var detailsOpts = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = lobbyId,
                LocalUserId = GetLocalUserId(),
            };

            if (lobbyInterface.CopyLobbyDetailsHandle(ref detailsOpts, out var details) != Result.Success)
                return "";

            // A3 audit: try/finally protege Release contra exceptions em CopyMemberAttributeByKey
            // ou ProductUserId.FromString.
            try
            {
                var attrOpts = new LobbyDetailsCopyMemberAttributeByKeyOptions
                {
                    TargetUserId = ProductUserId.FromString(userId),
                    AttrKey = MemberAttributes.DISPLAY_NAME,
                };

                if (details.CopyMemberAttributeByKey(ref attrOpts, out var attr) == Result.Success && attr.HasValue)
                    return attr.Value.Data?.Value.AsUtf8 ?? "";

                return "";
            }
            finally
            {
                details.Release();
            }
        }

        // EOS nao emite Joined para membros preexistentes — itera manualmente
        private void PopulateMembersFromDetails(LobbyDetails details, string hostUserId)
        {
            string localUserId = SessionManager.Instance.GetUserId();

            var countOpts = new LobbyDetailsGetMemberCountOptions();
            uint count = details.GetMemberCount(ref countOpts);

            for (uint i = 0; i < count; i++)
            {
                var byIndexOpts = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                var memberId = details.GetMemberByIndex(ref byIndexOpts);
                if (memberId == null) continue;

                string userId = memberId.ToString();
                bool isHost = userId == hostUserId;
                string displayName;

                // Jogador local: usa nome da sessao (mais confiavel que o atributo ainda nao definido)
                if (userId == localUserId)
                {
                    displayName = SessionManager.Instance.GetDisplayName();
                }
                else
                {
                    var attrOpts = new LobbyDetailsCopyMemberAttributeByKeyOptions
                    {
                        TargetUserId = memberId,
                        AttrKey = MemberAttributes.DISPLAY_NAME,
                    };
                    displayName = "";
                    if (details.CopyMemberAttributeByKey(ref attrOpts, out var attr) == Result.Success && attr.HasValue)
                        displayName = attr.Value.Data?.Value.AsUtf8 ?? "";
                }

                if (string.IsNullOrEmpty(displayName))
                    displayName = isHost ? "Host" : (userId.Length > 8 ? $"Jogador_{userId.Substring(0, 8)}" : userId);

                if (!_members.Exists(m => m.productUserId == userId))
                    _members.Add(new LobbyMember(userId, displayName, host: isHost));
            }

            Debug.Log($"[LobbyManager] Membros carregados da sala: {_members.Count}");
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
#endif

        private void ClearLobbyState()
        {
            CancelPendingClientConnect();
            _isInLobby = false;
            _currentLobby = null;
            _members.Clear();

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
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            return addr.Address.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Nao foi possivel obter IP local: {e.Message}");
            }
            return "127.0.0.1";
        }

        public bool IsInLobby() => _isInLobby;
        public LobbyInfo GetCurrentLobby() => _currentLobby;
        public List<LobbyMember> GetMembers() => GetOrderedMembers();

        public List<LobbyMember> GetOrderedMembers()
        {
            List<LobbyMember> orderedMembers = new List<LobbyMember>(_members);
            orderedMembers.Sort(CompareLobbyMembers);
            return orderedMembers;
        }

        public int GetCanonicalMemberIndex(string productUserId)
        {
            if (string.IsNullOrEmpty(productUserId))
                return -1;

            return GetOrderedMembers().FindIndex(member => member.productUserId == productUserId);
        }

        private static int CompareLobbyMembers(LobbyMember left, LobbyMember right)
        {
            bool leftIsHost = left != null && left.isHost;
            bool rightIsHost = right != null && right.isHost;

            if (leftIsHost != rightIsHost)
                return leftIsHost ? -1 : 1;

            string leftId = left?.productUserId ?? string.Empty;
            string rightId = right?.productUserId ?? string.Empty;
            return string.Compare(leftId, rightId, StringComparison.Ordinal);
        }
    }
}
