using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;

#if !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
#endif

using ExoBeasts.Multiplayer.Auth;

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

        public event Action<LobbyInfo>       OnLobbyCreated;
        public event Action<List<LobbyInfo>> OnLobbiesFound;
        public event Action<LobbyInfo>       OnLobbyJoined;
        public event Action                  OnLobbyLeft;
        public event Action<LobbyMember>     OnMemberJoined;
        public event Action<LobbyMember>     OnMemberLeft;
        public event Action<LobbyMember>     OnMemberUpdated;
        public event Action<string>          OnError;

        private LobbyInfo         _currentLobby;
        private bool              _isInLobby;
        private List<LobbyMember> _members = new List<LobbyMember>();

        // EOS exige LobbyDetails handle para JoinLobby, nao apenas string ID
        private readonly Dictionary<string, LobbyDetails> _detailsCache =
            new Dictionary<string, LobbyDetails>();

        private ulong _memberStatusHandle;
        private ulong _lobbyUpdateHandle;
        private ulong _memberUpdateHandle;

        // Cache do EOSManagerWrapper para evitar lazy-create em OnDestroy
        private Core.EOSManagerWrapper _eosCache;

        private const ushort DEFAULT_PORT = 7777;
        private const string BUCKET_ID   = "ExoBeasts";

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
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
            return ProductUserId.FromString(SessionManager.Instance.GetUserId());
        }
#endif

        public void CreateLobby(LobbySettings settings)
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) { OnError?.Invoke("EOS nao inicializado"); return; }

            var localUserId = GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                OnError?.Invoke("Usuario nao autenticado. Faca login antes de criar um lobby.");
                return;
            }

            var options = new CreateLobbyOptions
            {
                LocalUserId          = localUserId,
                MaxLobbyMembers      = (uint)Mathf.Clamp(settings.maxPlayers, 2, 4),
                PermissionLevel      = settings.isPublic
                                         ? LobbyPermissionLevel.Publicadvertised
                                         : LobbyPermissionLevel.Inviteonly,
                BucketId             = BUCKET_ID,
                AllowInvites         = true,
                PresenceEnabled      = false,
                EnableJoinById       = true,
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
                _isInLobby = true;

                SetLobbyAttributes(lobbyId, settings, () =>
                {
                    _currentLobby = new LobbyInfo
                    {
                        lobbyId           = lobbyId,
                        lobbyName         = settings.lobbyName,
                        hostDisplayName   = SessionManager.Instance.GetDisplayName(),
                        hostProductUserId = SessionManager.Instance.GetUserId(),
                        currentPlayers    = 1,
                        maxPlayers        = settings.maxPlayers,
                        mapName           = settings.mapName,
                        isPublic          = settings.isPublic,
                        state             = LobbyState.WaitingForPlayers,
                    };

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
#else
            Debug.LogWarning("[LobbyManager] EOS desabilitado (EOS_DISABLE)");
#endif
        }

        private void SetLobbyAttributes(string lobbyId, LobbySettings settings, Action onComplete)
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            var localUserId    = GetLocalUserId();

            var modOptions = new UpdateLobbyModificationOptions
            {
                LocalUserId = localUserId,
                LobbyId     = lobbyId,
            };

            if (lobbyInterface.UpdateLobbyModification(ref modOptions, out var mod) != Result.Success)
            {
                Debug.LogError("[LobbyManager] Falha ao obter LobbyModification");
                onComplete?.Invoke();
                return;
            }

            AddStringAttr(mod, LobbyAttributes.LOBBY_NAME,     settings.lobbyName,                       LobbyAttributeVisibility.Public);
            AddStringAttr(mod, LobbyAttributes.MAP_NAME,       settings.mapName,                          LobbyAttributeVisibility.Public);
            AddInt64Attr (mod, LobbyAttributes.MAX_PLAYERS,    settings.maxPlayers,                       LobbyAttributeVisibility.Public);
            AddStringAttr(mod, LobbyAttributes.LOBBY_STATE,    LobbyState.WaitingForPlayers.ToString(),   LobbyAttributeVisibility.Public);
            // Campos reservados para StartMatch — clientes observam SERVER_ADDRESS
            AddStringAttr(mod, LobbyAttributes.SERVER_ADDRESS, "",            LobbyAttributeVisibility.Public);
            AddInt64Attr (mod, LobbyAttributes.SERVER_PORT,    DEFAULT_PORT,  LobbyAttributeVisibility.Public);

            var updateOpts = new UpdateLobbyOptions { LobbyModificationHandle = mod };
            lobbyInterface.UpdateLobby(ref updateOpts, null, (ref UpdateLobbyCallbackInfo info) =>
            {
                mod.Release();
                if (info.ResultCode != Result.Success)
                    Debug.LogWarning($"[LobbyManager] Atributos do lobby com erro: {info.ResultCode}");
                onComplete?.Invoke();
            });
#else
            onComplete?.Invoke();
#endif
        }

        public void SearchLobbies(LobbySearchFilter filter)
        {
#if !EOS_DISABLE
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
                        Key   = LobbyAttributes.LOBBY_NAME,
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
                uint count    = searchHandle.GetSearchResultCount(ref countOpts);
                var  results  = new List<LobbyInfo>();

                for (uint i = 0; i < count; i++)
                {
                    var copyOpts = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = i };
                    if (searchHandle.CopySearchResultByIndex(ref copyOpts, out var details) != Result.Success)
                        continue;

                    var infoOpts = new LobbyDetailsCopyInfoOptions();
                    if (details.CopyInfo(ref infoOpts, out var di) != Result.Success || !di.HasValue)
                    {
                        details.Release();
                        continue;
                    }

                    string ownerUserId = di.Value.LobbyOwnerUserId?.ToString() ?? "";
                    var lobby = new LobbyInfo
                    {
                        lobbyId           = di.Value.LobbyId,
                        hostDisplayName   = ownerUserId,
                        hostProductUserId = ownerUserId,
                        maxPlayers        = (int)di.Value.MaxMembers,
                        currentPlayers    = (int)(di.Value.MaxMembers - di.Value.AvailableSlots),
                        isPublic          = di.Value.PermissionLevel == LobbyPermissionLevel.Publicadvertised,
                    };

                    var attrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.LOBBY_NAME };
                    if (details.CopyAttributeByKey(ref attrOpts, out var nameAttr) == Result.Success && nameAttr.HasValue)
                        lobby.lobbyName = nameAttr.Value.Data?.Value.AsUtf8 ?? "Sala";

                    attrOpts.AttrKey = LobbyAttributes.MAP_NAME;
                    if (details.CopyAttributeByKey(ref attrOpts, out var mapAttr) == Result.Success && mapAttr.HasValue)
                        lobby.mapName = mapAttr.Value.Data?.Value.AsUtf8 ?? "";

                    results.Add(lobby);
                    _detailsCache[di.Value.LobbyId] = details;
                }

                searchHandle.Release();
                Debug.Log($"[LobbyManager] Busca concluida: {results.Count} lobbies encontrados");
                OnLobbiesFound?.Invoke(results);
            });
#else
            OnLobbiesFound?.Invoke(new List<LobbyInfo>());
#endif
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
                LocalUserId        = localUserId,
                PresenceEnabled    = false,
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
                        LobbyId     = lobbyId,
                        LocalUserId = GetLocalUserId(),
                    };
                    if (GetLobbyInterface()?.CopyLobbyDetailsHandle(ref freshOpts, out var freshDetails) == Result.Success)
                    {
                        PopulateMembersFromDetails(freshDetails, lobbyInfo.hostProductUserId);
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
            var localUserId    = GetLocalUserId();
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
                LobbyId     = _currentLobby?.lobbyId ?? "",
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
        /// Define um atributo do jogador local no lobby atual.
        /// Ex: IS_READY=True, CHARACTER_INDEX=2
        /// </summary>
        public void SetMemberAttribute(string key, string value)
        {
            if (!_isInLobby || _currentLobby == null) return;

#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) return;

            var localUserId = GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                Debug.LogWarning("[LobbyManager] SetMemberAttribute chamado sem autenticacao");
                return;
            }

            var modOpts = new UpdateLobbyModificationOptions
            {
                LocalUserId = localUserId,
                LobbyId     = _currentLobby.lobbyId,
            };

            if (lobbyInterface.UpdateLobbyModification(ref modOpts, out var mod) != Result.Success)
            {
                Debug.LogError("[LobbyManager] Falha ao obter LobbyModification para atributo de membro");
                return;
            }

            AddStringMemberAttr(mod, key, value, LobbyAttributeVisibility.Public);

            var updateOpts = new UpdateLobbyOptions { LobbyModificationHandle = mod };
            lobbyInterface.UpdateLobby(ref updateOpts, null, (ref UpdateLobbyCallbackInfo info) =>
            {
                mod.Release();
                if (info.ResultCode == Result.Success)
                    Debug.Log($"[LobbyManager] Atributo definido: {key} = {value}");
                else
                    Debug.LogWarning($"[LobbyManager] Falha ao definir '{key}': {info.ResultCode}");
            });
#endif
        }

        public void SetReady(bool ready)
            => SetMemberAttribute(MemberAttributes.IS_READY, ready.ToString());

        public void SelectCharacter(int characterIndex)
            => SetMemberAttribute(MemberAttributes.CHARACTER_INDEX, characterIndex.ToString());

        /// <summary>
        /// Inicia a partida como Host:
        ///   1. Inicia NGO Host
        ///   2. Publica SERVER_ADDRESS + SERVER_PORT como atributos do lobby
        ///   3. Carrega a cena de jogo via NGO SceneManager (todos os clientes seguem)
        ///
        /// Clientes recebem OnLobbyAttributeUpdated, leem SERVER_ADDRESS e chamam StartClient.
        /// </summary>
        public void StartMatch()
        {
            if (!_isInLobby || _currentLobby == null)
            {
                Debug.LogWarning("[LobbyManager] StartMatch chamado fora de um lobby");
                return;
            }

            Debug.Log("[LobbyManager] Iniciando partida como HOST...");

            if (!NetworkManager.Singleton.StartHost())
            {
                OnError?.Invoke("Falha ao iniciar Host NGO");
                return;
            }

            string localIp = GetLocalIpAddress();
            ushort port    = DEFAULT_PORT;
            var transport  = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", port);
                port = transport.ConnectionData.Port;
            }

            Debug.Log($"[LobbyManager] Host NGO ativo. Publicando no lobby: {localIp}:{port}");

#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();
            var modOpts = new UpdateLobbyModificationOptions
            {
                LocalUserId = GetLocalUserId(),
                LobbyId     = _currentLobby.lobbyId,
            };

            if (lobbyInterface.UpdateLobbyModification(ref modOpts, out var mod) != Result.Success)
            {
                Debug.LogError("[LobbyManager] Falha ao obter LobbyModification para StartMatch");
                return;
            }

            AddStringAttr(mod, LobbyAttributes.SERVER_ADDRESS, localIp,                         LobbyAttributeVisibility.Public);
            AddInt64Attr (mod, LobbyAttributes.SERVER_PORT,    port,                             LobbyAttributeVisibility.Public);
            AddStringAttr(mod, LobbyAttributes.LOBBY_STATE,   LobbyState.InGame.ToString(),     LobbyAttributeVisibility.Public);

            var updateOpts = new UpdateLobbyOptions { LobbyModificationHandle = mod };
            lobbyInterface.UpdateLobby(ref updateOpts, null, (ref UpdateLobbyCallbackInfo info) =>
            {
                mod.Release();
                if (info.ResultCode == Result.Success)
                {
                    Debug.Log($"[LobbyManager] Endereco publicado. Carregando cena '{_currentLobby.mapName}'...");
                    NetworkManager.Singleton.SceneManager.LoadScene(
                        _currentLobby.mapName, LoadSceneMode.Single);
                }
                else
                {
                    Debug.LogError($"[LobbyManager] Falha ao publicar endereco: {info.ResultCode}");
                    OnError?.Invoke($"Falha ao iniciar partida: {info.ResultCode}");
                    // Rollback: desligar o Host NGO pois clientes nao vao receber SERVER_ADDRESS
                    NetworkManager.Singleton.Shutdown();
                }
            });
#endif
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
            if (!_isInLobby || _currentLobby == null) return;
            if (info.LobbyId != _currentLobby.lobbyId) return;

            string userId = info.TargetUserId?.ToString() ?? "";
            var member = _members.Find(m => m.productUserId == userId);
            if (member == null) return;

            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) return;

            var detailsOpts = new CopyLobbyDetailsHandleOptions
            {
                LobbyId     = info.LobbyId,
                LocalUserId = GetLocalUserId(),
            };

            if (lobbyInterface.CopyLobbyDetailsHandle(ref detailsOpts, out var details) != Result.Success)
                return;

            bool oldReady = member.isReady;

            var readyOpts = new LobbyDetailsCopyMemberAttributeByKeyOptions
            {
                TargetUserId = info.TargetUserId,
                AttrKey      = MemberAttributes.IS_READY,
            };
            if (details.CopyMemberAttributeByKey(ref readyOpts, out var readyAttr) == Result.Success && readyAttr.HasValue)
                bool.TryParse(readyAttr.Value.Data?.Value.AsUtf8, out member.isReady);

            // Atualizar displayName silenciosamente se ainda era um ID curto (fallback)
            var nameOpts = new LobbyDetailsCopyMemberAttributeByKeyOptions
            {
                TargetUserId = info.TargetUserId,
                AttrKey      = MemberAttributes.DISPLAY_NAME,
            };
            if (details.CopyMemberAttributeByKey(ref nameOpts, out var nameAttr) == Result.Success && nameAttr.HasValue)
            {
                string newName = nameAttr.Value.Data?.Value.AsUtf8 ?? "";
                if (!string.IsNullOrEmpty(newName))
                    member.displayName = newName;
            }

            details.Release();

            // Notificar UI apenas quando IS_READY muda de valor
            // (evita notificacao falsa quando DISPLAY_NAME e atualizado no join)
            if (member.isReady != oldReady)
            {
                Debug.Log($"[LobbyManager] Membro atualizado: {userId} | isReady={member.isReady} | nome={member.displayName}");
                OnMemberUpdated?.Invoke(member);
            }
            else
            {
                Debug.Log($"[LobbyManager] Atributo de membro atualizado (sem mudanca de ready): {userId} | nome={member.displayName}");
            }
        }

        // Chamado quando atributos do lobby mudam (clientes detectam SERVER_ADDRESS aqui)
        private void OnLobbyAttributeUpdated(ref LobbyUpdateReceivedCallbackInfo info)
        {
            if (!_isInLobby || _currentLobby == null) return;
            if (info.LobbyId != _currentLobby.lobbyId) return;

            // Apenas clientes que ainda nao estao conectados ao NGO
            if (NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
                return;

            var lobbyInterface = GetLobbyInterface();
            var detailsOpts = new CopyLobbyDetailsHandleOptions
            {
                LobbyId     = info.LobbyId,
                LocalUserId = GetLocalUserId(),
            };

            if (lobbyInterface.CopyLobbyDetailsHandle(ref detailsOpts, out var details) != Result.Success)
                return;

            var attrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.SERVER_ADDRESS };
            if (details.CopyAttributeByKey(ref attrOpts, out var addrAttr) == Result.Success && addrAttr.HasValue)
            {
                string serverAddress = addrAttr.Value.Data?.Value.AsUtf8 ?? "";
                if (!string.IsNullOrEmpty(serverAddress))
                {
                    ushort port = DEFAULT_PORT;
                    attrOpts.AttrKey = LobbyAttributes.SERVER_PORT;
                    if (details.CopyAttributeByKey(ref attrOpts, out var portAttr) == Result.Success && portAttr.HasValue)
                        port = (ushort)(portAttr.Value.Data?.Value.AsInt64 ?? DEFAULT_PORT);

                    Debug.Log($"[LobbyManager] Partida iniciada pelo host! Conectando: {serverAddress}:{port}");
                    var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
                    if (transport != null)
                    {
                        transport.SetConnectionData(serverAddress, port);
                        NetworkManager.Singleton.StartClient();
                    }
                }
            }

            details.Release();
        }
#endif

#if !EOS_DISABLE
        private static void AddStringAttr(LobbyModification mod, string key, string value, LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddAttributeOptions
            {
                Attribute  = new AttributeData { Key = key, Value = new AttributeDataValue { AsUtf8 = value } },
                Visibility = vis,
            };
            mod.AddAttribute(ref opts);
        }

        private static void AddInt64Attr(LobbyModification mod, string key, long value, LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddAttributeOptions
            {
                Attribute  = new AttributeData { Key = key, Value = new AttributeDataValue { AsInt64 = value } },
                Visibility = vis,
            };
            mod.AddAttribute(ref opts);
        }

        private static void AddStringMemberAttr(LobbyModification mod, string key, string value, LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddMemberAttributeOptions
            {
                Attribute  = new AttributeData { Key = key, Value = new AttributeDataValue { AsUtf8 = value } },
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
                LobbyId     = lobbyId,
                LocalUserId = GetLocalUserId(),
            };

            if (lobbyInterface.CopyLobbyDetailsHandle(ref detailsOpts, out var details) != Result.Success)
                return "";

            var attrOpts = new LobbyDetailsCopyMemberAttributeByKeyOptions
            {
                TargetUserId = ProductUserId.FromString(userId),
                AttrKey      = MemberAttributes.DISPLAY_NAME,
            };

            string result = "";
            if (details.CopyMemberAttributeByKey(ref attrOpts, out var attr) == Result.Success && attr.HasValue)
                result = attr.Value.Data?.Value.AsUtf8 ?? "";

            details.Release();
            return result;
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

                string userId  = memberId.ToString();
                bool   isHost  = userId == hostUserId;
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
                        AttrKey      = MemberAttributes.DISPLAY_NAME,
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
                result.maxPlayers        = (int)di.Value.MaxMembers;
                result.currentPlayers    = (int)(di.Value.MaxMembers - di.Value.AvailableSlots);
                result.isPublic          = di.Value.PermissionLevel == LobbyPermissionLevel.Publicadvertised;
                result.hostProductUserId = di.Value.LobbyOwnerUserId?.ToString() ?? "";
            }

            var attrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.LOBBY_NAME };
            if (details.CopyAttributeByKey(ref attrOpts, out var nameAttr) == Result.Success && nameAttr.HasValue)
                result.lobbyName = nameAttr.Value.Data?.Value.AsUtf8 ?? "Sala";

            attrOpts.AttrKey = LobbyAttributes.MAP_NAME;
            if (details.CopyAttributeByKey(ref attrOpts, out var mapAttr) == Result.Success && mapAttr.HasValue)
                result.mapName = mapAttr.Value.Data?.Value.AsUtf8 ?? "";

            onResult?.Invoke(result);
        }
#endif

        private void ClearLobbyState()
        {
            _isInLobby    = false;
            _currentLobby = null;
            _members.Clear();
            SessionManager.Instance.SetCurrentLobby("");
            ReleaseDetailCache();
        }

        private void ReleaseDetailCache()
        {
#if !EOS_DISABLE
            foreach (var d in _detailsCache.Values)
                d.Release();
#endif
            _detailsCache.Clear();
        }

        private static string GetLocalIpAddress()
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

        public bool IsInLobby()               => _isInLobby;
        public LobbyInfo GetCurrentLobby()    => _currentLobby;
        public List<LobbyMember> GetMembers() => _members;
    }
}
