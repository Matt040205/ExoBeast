using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.GameServer;
using ExoBeasts.Multiplayer.Lobby;

namespace ExoBeasts.Multiplayer.Sync
{
    public static class NetworkGameplayResolver
    {
        public static bool TryResolveCharacterData(
            Component context,
            out CharacterBase characterData,
            int preferredIndex = -1,
            bool allowOwnerLocalFallback = true)
        {
            characterData = ResolveCharacterData(context, preferredIndex, allowOwnerLocalFallback);
            return characterData != null;
        }

        public static CharacterBase ResolveCharacterData(
            Component context,
            int preferredIndex = -1,
            bool allowOwnerLocalFallback = true)
        {
            if (!TryResolveCharacterIndex(context, out int characterIndex, preferredIndex, allowOwnerLocalFallback))
                return null;

            return ResolveCharacterDataByIndex(characterIndex);
        }

        public static bool TryResolveCharacterIndex(
            Component context,
            out int characterIndex,
            int preferredIndex = -1,
            bool allowOwnerLocalFallback = true)
        {
            characterIndex = preferredIndex;
            if (IsValidCharacterIndex(characterIndex))
                return true;

            if (context == null)
                return false;

            NetworkedPlayerController networkedPlayer = context.GetComponent<NetworkedPlayerController>();
            if (networkedPlayer != null && IsValidCharacterIndex(networkedPlayer.CharacterIndex.Value))
            {
                characterIndex = networkedPlayer.CharacterIndex.Value;
                return true;
            }

            NetworkObject networkObject = context.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                if (PlayerRegistry.Instance != null &&
                    PlayerRegistry.Instance.GetAllPlayers().ContainsKey(networkObject.OwnerClientId))
                {
                    characterIndex = PlayerRegistry.Instance.GetPlayerCharacterChoice(networkObject.OwnerClientId);
                    if (characterIndex >= 0)
                        return true;
                }

                if (CharacterChoiceCache.TryGet(networkObject.OwnerClientId, out int cachedIndex) && cachedIndex >= 0)
                {
                    characterIndex = cachedIndex;
                    return true;
                }

                if (allowOwnerLocalFallback && (!networkObject.IsSpawned || networkObject.IsOwner))
                {
                    if (TryResolveLocalCommanderCharacterIndex(out int localIndex))
                    {
                        characterIndex = localIndex;
                        return true;
                    }
                }

                return false;
            }

            if (allowOwnerLocalFallback && TryResolveLocalCommanderCharacterIndex(out int fallbackIndex))
            {
                characterIndex = fallbackIndex;
                return true;
            }

            return false;
        }

        public static CharacterBase ResolveCharacterDataByIndex(int characterIndex)
        {
            if (!IsValidCharacterIndex(characterIndex))
                return null;

            if (GameDataManager.Instance?.bibliotecaOriginalPersonagens == null ||
                characterIndex >= GameDataManager.Instance.bibliotecaOriginalPersonagens.Count)
            {
                return null;
            }

            return GameDataManager.Instance.bibliotecaOriginalPersonagens[characterIndex];
        }

        public static bool TryResolveAttackerFromPlayer(
            GameObject owner,
            out ulong attackerClientId,
            out PlayerHealthSystem attackerHealth)
        {
            attackerClientId = NetworkManager.ServerClientId;
            attackerHealth = null;

            if (owner == null)
                return false;

            attackerHealth = owner.GetComponent<PlayerHealthSystem>();

            if (owner.TryGetComponent(out NetworkObject networkObject))
            {
                attackerClientId = networkObject.OwnerClientId;
                return true;
            }

            if (NetworkManager.Singleton != null)
            {
                attackerClientId = NetworkManager.Singleton.LocalClientId;
                return attackerHealth != null;
            }

            return attackerHealth != null;
        }

        public static bool TryResolveAttackerFromBuilding(
            Component context,
            out ulong attackerClientId,
            out PlayerHealthSystem attackerHealth)
        {
            attackerClientId = NetworkManager.ServerClientId;
            attackerHealth = null;

            if (context == null)
                return false;

            NetworkedBuilding networkedBuilding = context.GetComponentInParent<NetworkedBuilding>();
            if (networkedBuilding == null)
                return false;

            attackerClientId = networkedBuilding.BuilderClientId.Value;
            attackerHealth = ResolvePlayerHealth(attackerClientId);
            return true;
        }

        public static PlayerHealthSystem ResolvePlayerHealth(ulong clientId)
        {
            if (PlayerRegistry.Instance == null)
                return null;

            GameObject playerObject = PlayerRegistry.Instance.GetPlayerObject(clientId);
            return playerObject != null ? playerObject.GetComponent<PlayerHealthSystem>() : null;
        }

        private static bool TryResolveLocalCommanderCharacterIndex(out int characterIndex)
        {
            characterIndex = -1;

            CharacterBase localCharacter = ResolveLocalCommanderCharacter();
            if (localCharacter == null || GameDataManager.Instance?.bibliotecaOriginalPersonagens == null)
                return false;

            string cleanName = localCharacter.name.Replace("(Clone)", "");
            characterIndex = GameDataManager.Instance.bibliotecaOriginalPersonagens.FindIndex(
                character => character != null && character.name == cleanName);

            return characterIndex >= 0;
        }

        private static CharacterBase ResolveLocalCommanderCharacter()
        {
            CharacterBase[] equipe = GameDataManager.Instance?.equipeSelecionada;
            if (equipe == null || equipe.Length == 0)
                return null;

            int commanderSlot = 0;
            LobbyManager lobbyManager = LobbyManager.Instance;
            SessionManager sessionManager = SessionManager.Instance;

            if (lobbyManager != null && sessionManager != null)
            {
                string localUserId = sessionManager.GetUserId();
                int localMemberIndex = lobbyManager.GetCanonicalMemberIndex(localUserId);
                int totalMembers = lobbyManager.GetOrderedMembers().Count;

                if (localMemberIndex >= 0)
                    commanderSlot = PartySlotLayout.GetCommanderSlot(totalMembers, localMemberIndex);
            }

            if (commanderSlot >= 0 &&
                commanderSlot < equipe.Length &&
                equipe[commanderSlot] != null)
            {
                return equipe[commanderSlot];
            }

            return equipe[0];
        }

        private static bool IsValidCharacterIndex(int characterIndex)
        {
            return GameDataManager.Instance?.bibliotecaOriginalPersonagens != null &&
                   characterIndex >= 0 &&
                   characterIndex < GameDataManager.Instance.bibliotecaOriginalPersonagens.Count;
        }
    }
}
