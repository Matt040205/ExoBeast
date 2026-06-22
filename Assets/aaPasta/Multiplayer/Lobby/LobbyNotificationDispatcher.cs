using System;
using System.Collections.Generic;
using UnityEngine;
#if !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using PlayEveryWare.EpicOnlineServices;
#endif

namespace ExoBeasts.Multiplayer.Lobby
{
    public class LobbyNotificationDispatcher
    {
        private readonly LobbyManager _lobbyManager;

#if !EOS_DISABLE
        private ulong _memberStatusHandle;
        private ulong _lobbyUpdateHandle;
        private ulong _memberUpdateHandle;
#endif

        public LobbyNotificationDispatcher(LobbyManager lobbyManager)
        {
            _lobbyManager = lobbyManager;
        }

        public void RegisterNotifications()
        {
#if !EOS_DISABLE
            if (_memberStatusHandle != 0 || _lobbyUpdateHandle != 0 || _memberUpdateHandle != 0)
            {
                Debug.Log("[LobbyNotificationDispatcher] Notificacoes EOS ja registradas");
                return;
            }

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

            Debug.Log("[LobbyNotificationDispatcher] Notificacoes EOS registradas");
#endif
        }

        public void UnregisterNotifications()
        {
#if !EOS_DISABLE
            var lobbyInterface = GetLobbyInterface();

            if (lobbyInterface != null && _memberStatusHandle != 0)
                lobbyInterface.RemoveNotifyLobbyMemberStatusReceived(_memberStatusHandle);
            if (lobbyInterface != null && _lobbyUpdateHandle != 0)
                lobbyInterface.RemoveNotifyLobbyUpdateReceived(_lobbyUpdateHandle);
            if (lobbyInterface != null && _memberUpdateHandle != 0)
                lobbyInterface.RemoveNotifyLobbyMemberUpdateReceived(_memberUpdateHandle);

            _memberStatusHandle = 0;
            _lobbyUpdateHandle = 0;
            _memberUpdateHandle = 0;
#endif
        }

#if !EOS_DISABLE
        private void OnMemberStatusChanged(ref LobbyMemberStatusReceivedCallbackInfo info)
        {
            if (!_lobbyManager.IsInLobby() || _lobbyManager.GetCurrentLobby() == null) return;
            if (info.LobbyId != _lobbyManager.GetCurrentLobby().lobbyId) return;

            string userId = info.TargetUserId?.ToString() ?? "";

            switch (info.CurrentStatus)
            {
                case LobbyMemberStatus.Joined:
                    if (_lobbyManager.FindMutableMember(userId) == null)
                    {
                        // Tentar ler o DISPLAY_NAME do atributo de membro (definido pelo cliente ao entrar)
                        // Pode nao estar disponivel imediatamente — fallback para ID curto
                        string displayName = _lobbyManager._membershipService.ReadMemberDisplayName(info.LobbyId, userId);
                        if (string.IsNullOrEmpty(displayName))
                            displayName = userId.Length > 8 ? $"Jogador_{userId.Substring(0, 8)}" : userId;

                        var currentLobby = _lobbyManager.GetCurrentLobby();
                        var member = new LobbyMember(
                            userId,
                            displayName,
                            currentLobby != null && currentLobby.hostProductUserId == userId);

                        if (_lobbyManager.TryAddMemberFromNotification(member))
                            _lobbyManager.InvokeOnMemberJoined(member);
                    }
                    Debug.Log($"[LobbyNotificationDispatcher] Membro entrou: {userId}");
                    break;

                case LobbyMemberStatus.Left:
                case LobbyMemberStatus.Disconnected:
                case LobbyMemberStatus.Kicked:
                    var leaving = _lobbyManager.TryRemoveMemberFromNotification(userId);
                    if (leaving != null)
                        _lobbyManager.InvokeOnMemberLeft(leaving);
                    Debug.Log($"[LobbyNotificationDispatcher] Membro saiu ({info.CurrentStatus}): {userId}");
                    break;

                case LobbyMemberStatus.Closed:
                    Debug.Log("[LobbyNotificationDispatcher] Lobby fechado pelo host");
                    _lobbyManager.ClearLobbyState();
                    _lobbyManager.InvokeOnLobbyLeft();
                    break;
            }
        }

        // Chamado quando atributos de UM MEMBRO mudam (ex: IS_READY, CHARACTER_INDEX)
        private void OnMemberAttributeChanged(ref LobbyMemberUpdateReceivedCallbackInfo info)
        {
            if (ExoBeasts.Managers.GameModeManager.CurrentMode != ExoBeasts.Managers.GameMode.Multiplayer)
            {
                _lobbyManager.CancelPendingClientConnect();
                return;
            }

            if (!_lobbyManager.IsInLobby() || _lobbyManager.GetCurrentLobby() == null) return;
            if (info.LobbyId != _lobbyManager.GetCurrentLobby().lobbyId) return;

            string userId = info.TargetUserId?.ToString() ?? "";
            var member = _lobbyManager.FindMutableMember(userId);
            if (member == null) return;

            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null) return;

            var localUserId = _lobbyManager.GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                Debug.LogWarning("[LobbyNotificationDispatcher] MemberUpdate ignorado: LocalUserId invalido");
                return;
            }

            var detailsOpts = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = info.LobbyId,
                LocalUserId = localUserId,
            };

            if (lobbyInterface.CopyLobbyDetailsHandle(ref detailsOpts, out var details) != Result.Success)
                return;

            bool oldReady = member.isReady;
            string oldDisplayName = member.displayName;
            int oldCharacterIndex = member.selectedCharacterIndex;

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
            if (member.isReady != oldReady ||
                member.displayName != oldDisplayName ||
                member.selectedCharacterIndex != oldCharacterIndex)
            {
                Debug.Log($"[LobbyNotificationDispatcher] Membro atualizado: {userId} | isReady={member.isReady} | nome={member.displayName} | char={member.selectedCharacterIndex}");
                _lobbyManager.InvokeOnMemberUpdated(member);
            }
        }

        // Chamado quando atributos do lobby mudam (clientes detectam SERVER_ADDRESS aqui)
        private void OnLobbyAttributeUpdated(ref LobbyUpdateReceivedCallbackInfo info)
        {
            if (ExoBeasts.Managers.GameModeManager.CurrentMode != ExoBeasts.Managers.GameMode.Multiplayer)
            {
                _lobbyManager.CancelPendingClientConnect();
                return;
            }

            Debug.Log($"[LobbyNotificationDispatcher][DBG] OnLobbyAttributeUpdated — LobbyId={info.LobbyId} | _isInLobby={_lobbyManager.IsInLobby()} | currentLobby={_lobbyManager.GetCurrentLobby()?.lobbyId ?? "null"}");

            if (!_lobbyManager.IsInLobby() || _lobbyManager.GetCurrentLobby() == null)
            {
                Debug.LogWarning("[LobbyNotificationDispatcher][DBG] Ignorado: nao esta em lobby ou _currentLobby nulo");
                return;
            }
            if (info.LobbyId != _lobbyManager.GetCurrentLobby().lobbyId)
            {
                Debug.LogWarning($"[LobbyNotificationDispatcher][DBG] Ignorado: LobbyId nao corresponde ({info.LobbyId} != {_lobbyManager.GetCurrentLobby().lobbyId})");
                return;
            }

            var lobbyInterface = GetLobbyInterface();
            if (lobbyInterface == null)
            {
                Debug.LogWarning("[LobbyNotificationDispatcher][DBG] LobbyInterface nula em OnLobbyAttributeUpdated");
                return;
            }

            var localUserId = _lobbyManager.GetLocalUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                Debug.LogWarning("[LobbyNotificationDispatcher][DBG] OnLobbyAttributeUpdated ignorado: LocalUserId invalido");
                return;
            }

            var detailsOpts = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = info.LobbyId,
                LocalUserId = localUserId,
            };

            var copyResult = lobbyInterface.CopyLobbyDetailsHandle(ref detailsOpts, out var details);
            if (copyResult != Result.Success)
            {
                Debug.LogError($"[LobbyNotificationDispatcher][DBG] CopyLobbyDetailsHandle falhou: {copyResult}");
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
        /// [SYNC-FIX] Extrai atributos de rede do lobby e inicia conexao se STATE=InGame.
        /// Centralizado para ser chamado por notificacoes e proativamente no Join.
        /// </summary>
        public void ProcessLobbyAttributes(LobbyDetails details)
        {
            if (details == null) return;

            if (ExoBeasts.Managers.GameModeManager.CurrentMode != ExoBeasts.Managers.GameMode.Multiplayer)
            {
                _lobbyManager.CancelPendingClientConnect();
                return;
            }

            // O host do lobby EOS nunca conecta como cliente NGO.
            string _myUid = ExoBeasts.Multiplayer.Auth.SessionManager.Instance?.GetUserId() ?? "";
            if (!string.IsNullOrEmpty(_myUid) && _lobbyManager.GetCurrentLobby() != null && _lobbyManager.GetCurrentLobby().hostProductUserId == _myUid)
            {
                return;
            }

            // Ja conectado como cliente ativo num jogo real — nao reconectar.
            // IsConnectedClient so vira true apos handshake completo com o servidor.
            // IsHost=true sozinho NAO bloqueia: pode ser um StartHost() do MenuScene que
            // precisa ser derrubado — ConnectClientCoroutine faz o Shutdown() antes de StartClient().
            if (Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.IsClient &&
                !Unity.Netcode.NetworkManager.Singleton.IsHost &&
                Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)
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
                    // Se nao esta em InGame ou Starting, ignora (ainda esperando)
                    return;
                }
                Debug.Log($"[LobbyNotificationDispatcher][DBG] Lobby em estado '{stateStr}' — processando dados de conexao...");
            }

            // Verificar RELAY_CODE primeiro
            var relayAttrOpts = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttributes.RELAY_CODE };
            var relayAttrResult = details.CopyAttributeByKey(ref relayAttrOpts, out var relayAttr);
            if (relayAttrResult == Result.Success && relayAttr.HasValue)
            {
                string relayCode = relayAttr.Value.Data?.Value.AsUtf8 ?? "";
                if (ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.IsUsableRelayCode(relayCode))
                {
                    Debug.Log($"[LobbyNotificationDispatcher] Conectando via Relay: {relayCode}");
                    int myChar = _lobbyManager.GetMyCharacterIndex();
                    ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.Instance.ConnectAsClientViaRelay(
                        relayCode, myChar, (err) => _lobbyManager.InvokeOnError(err));
                    return;
                }
                else if (!string.IsNullOrEmpty(relayCode))
                {
                    Debug.Log("[LobbyNotificationDispatcher] RELAY_CODE sentinel/invalidado. Usando fallback SERVER_ADDRESS.");
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

                    Debug.Log($"[LobbyNotificationDispatcher] Conectando via IP: {serverAddress}:{port}");
                    int myChar = _lobbyManager.GetMyCharacterIndex();
                    ExoBeasts.Multiplayer.GameServer.MatchSessionLauncher.Instance.ConnectAsClientViaIp(
                        serverAddress, port, myChar, (err) => _lobbyManager.InvokeOnError(err));
                }
            }
        }

        private LobbyInterface GetLobbyInterface()
        {
            return EOSManager.Instance.GetEOSPlatformInterface()?.GetLobbyInterface();
        }
#endif
    }
}

