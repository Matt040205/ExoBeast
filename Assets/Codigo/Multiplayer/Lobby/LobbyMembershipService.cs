using System;
using System.Collections.Generic;
using UnityEngine;

#if !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
#endif

using ExoBeasts.Multiplayer.Auth;

namespace ExoBeasts.Multiplayer.Lobby
{
    public class LobbyMembershipService
    {
        private List<LobbyMember> _members = new List<LobbyMember>();
        private LobbyManager _manager;

        public LobbyMembershipService(LobbyManager manager)
        {
            _manager = manager;
        }

        public void Clear()
        {
            _members.Clear();
        }

        internal void AddMember(LobbyMember member)
        {
            if (member != null && !_members.Exists(m => m.productUserId == member.productUserId))
                _members.Add(member);
        }

        public List<LobbyMember> GetMembers()
        {
            return GetOrderedMembers();
        }

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

        internal LobbyMember FindMutableMember(string productUserId)
        {
            if (string.IsNullOrEmpty(productUserId))
                return null;

            return _members.Find(member => member.productUserId == productUserId);
        }

        internal bool TryAddMemberFromNotification(LobbyMember member)
        {
            if (member == null || string.IsNullOrEmpty(member.productUserId))
                return false;

            if (FindMutableMember(member.productUserId) != null)
                return false;

            var currentLobby = _manager.GetCurrentLobby();
            if (currentLobby != null && member.productUserId == currentLobby.hostProductUserId)
                member.isHost = true;

            _members.Add(member);
            RefreshCurrentPlayerCountFromMembers();
            return true;
        }

        internal LobbyMember TryRemoveMemberFromNotification(string productUserId)
        {
            var member = FindMutableMember(productUserId);
            if (member == null)
                return null;

            _members.Remove(member);
            RefreshCurrentPlayerCountFromMembers();
            return member;
        }

        internal void RefreshCurrentPlayerCountFromMembers()
        {
            var currentLobby = _manager.GetCurrentLobby();
            if (currentLobby != null)
                currentLobby.currentPlayers = _members.Count;
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

#if !EOS_DISABLE
        // EOS nao emite Joined para membros preexistentes — itera manualmente
        public void PopulateMembersFromDetails(LobbyDetails details, string hostUserId)
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

            Debug.Log($"[LobbyMembershipService] Membros carregados da sala: {_members.Count}");
        }

        public string ReadMemberDisplayName(string lobbyId, string userId)
        {
            var lobbyInterface = PlayEveryWare.EpicOnlineServices.EOSManager.Instance?.GetEOSPlatformInterface()?.GetLobbyInterface();
            if (lobbyInterface == null) return "";

            var localUserIdStr = SessionManager.Instance?.GetUserId();
            if (string.IsNullOrEmpty(localUserIdStr)) return "";
            var localUserId = ProductUserId.FromString(localUserIdStr);
            if (localUserId == null || !localUserId.IsValid())
                return "";

            var detailsOpts = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = lobbyId,
                LocalUserId = localUserId,
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
#endif
    }
}
