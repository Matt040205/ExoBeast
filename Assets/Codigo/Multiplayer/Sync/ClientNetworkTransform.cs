using Unity.Netcode.Components;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// NetworkTransform client-authoritative para jogadores controlados localmente.
    /// Mantem compatibilidade com o nome ja usado pelos prefabs e scripts do projeto.
    /// </summary>
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
