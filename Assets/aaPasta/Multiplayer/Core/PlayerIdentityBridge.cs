using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.GameServer;
using ExoBeasts.Multiplayer.Auth;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// ── PlayerIdentityBridge ──────────────────────────────────
    /// Ponte entre NGO (clientId) e EOS (productUserId + sessionToken).
    ///
    ///  ▸ Singleton NetworkBehaviour — deve existir na cena ANTES dos players spawnarem
    ///  ▸ Cada jogador chama RegisterPlayerServerRpc no OnNetworkSpawn (via PlayerNetworkSetup)
    ///  ▸ Servidor armazena mapeamento clientId → (userId, token)
    ///  ▸ PlayerRegistry recebe o link para mapeamento bidirecional
    /// ─────────────────────────────────────────────────────────
    /// </summary>
    public class PlayerIdentityBridge : NetworkBehaviour
    {
        public static PlayerIdentityBridge Instance { get; private set; }

        private readonly Dictionary<ulong, PlayerIdentity> _identities = new Dictionary<ulong, PlayerIdentity>();

        public struct PlayerIdentity
        {
            public string productUserId;
            public string sessionToken;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Chamado por cada jogador (IsOwner) no OnNetworkSpawn via PlayerNetworkSetup.
        /// Envia userId e sessionToken do EOS para o servidor vincular ao clientId do NGO.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RegisterPlayerServerRpc(string productUserId, string sessionToken, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            _identities[clientId] = new PlayerIdentity
            {
                productUserId = productUserId,
                sessionToken = sessionToken
            };

            string tokenPreview = string.IsNullOrEmpty(sessionToken) ? "(vazio)" : sessionToken.Substring(0, Mathf.Min(8, sessionToken.Length));
            Debug.Log($"[PlayerIdentityBridge] Registrado: ClientId={clientId} → UserId={productUserId} Token={tokenPreview}...");

            if (PlayerRegistry.Instance != null)
            {
                PlayerRegistry.Instance.LinkProductUserId(clientId, productUserId);
            }
        }

        /// <summary>
        /// Retorna a identidade EOS de um jogador pelo clientId do NGO.
        /// </summary>
        public PlayerIdentity? GetIdentity(ulong clientId)
        {
            if (_identities.TryGetValue(clientId, out var identity))
                return identity;
            return null;
        }

        /// <summary>
        /// Retorna o clientId NGO a partir do productUserId EOS.
        /// </summary>
        public ulong? GetClientIdByUserId(string productUserId)
        {
            foreach (var kvp in _identities)
            {
                if (kvp.Value.productUserId == productUserId)
                    return kvp.Key;
            }
            return null;
        }

        public override void OnNetworkDespawn()
        {
            _identities.Clear();
            base.OnNetworkDespawn();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
