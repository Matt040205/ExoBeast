#if !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// ── EosLobbyModHelper ─────────────────────────────────
    /// Wrappers de conveniência para adicionar atributos de lobby
    /// e de membro a um LobbyModification handle EOS.
    ///
    ///  ▸ AddStringAttr     — atributo de lobby com valor string
    ///  ▸ AddInt64Attr      — atributo de lobby com valor int64
    ///  ▸ AddStringMemberAttr — atributo de membro com valor string
    ///
    /// Antes deste helper, os três métodos eram privados e duplicados
    /// em LobbyManager e MatchSessionLauncher.
    /// ─────────────────────────────────────────────────────
    /// </summary>
    internal static class EosLobbyModHelper
    {
        public static void AddStringAttr(
            LobbyModification mod,
            string key,
            string value,
            LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddAttributeOptions
            {
                Attribute = new AttributeData
                {
                    Key = key,
                    Value = new AttributeDataValue { AsUtf8 = value },
                },
                Visibility = vis,
            };
            mod.AddAttribute(ref opts);
        }

        public static void AddInt64Attr(
            LobbyModification mod,
            string key,
            long value,
            LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddAttributeOptions
            {
                Attribute = new AttributeData
                {
                    Key = key,
                    Value = new AttributeDataValue { AsInt64 = value },
                },
                Visibility = vis,
            };
            mod.AddAttribute(ref opts);
        }

        public static void AddStringMemberAttr(
            LobbyModification mod,
            string key,
            string value,
            LobbyAttributeVisibility vis)
        {
            var opts = new LobbyModificationAddMemberAttributeOptions
            {
                Attribute = new AttributeData
                {
                    Key = key,
                    Value = new AttributeDataValue { AsUtf8 = value },
                },
                Visibility = vis,
            };
            mod.AddMemberAttribute(ref opts);
        }
    }
}
#endif
