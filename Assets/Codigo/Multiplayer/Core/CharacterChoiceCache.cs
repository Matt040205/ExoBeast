using System.Collections.Generic;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// ── CharacterChoiceCache ───────────────────────────────
    /// Cache estatico de escolhas de personagem por clientId.
    ///
    ///  ▸ Populado antes do Host/Client conectar ao NGO
    ///  ▸ Lido por GameSetupManager durante o spawn
    ///  ▸ Sobrevive a transicao de cena (static, nao depende de NetworkBehaviour)
    ///  ▸ Host usa ServerClientId (0UL) via HostCharacterIndex
    ///  ▸ Clientes sao registrados no ConnectionApprovalCallback
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public static class CharacterChoiceCache
    {
        public static int HostCharacterIndex = -1;
        public static readonly Dictionary<ulong, int> ByClientId = new Dictionary<ulong, int>();

        public static int Get(ulong clientId, int fallback = 0)
        {
            if (clientId == NetworkManager.ServerClientId && HostCharacterIndex >= 0)
                return HostCharacterIndex;
            return ByClientId.TryGetValue(clientId, out int i) ? i : fallback;
        }

        public static void Clear()
        {
            HostCharacterIndex = -1;
            ByClientId.Clear();
        }
    }
}
