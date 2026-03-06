using UnityEngine;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// ── MppmHelper ─────────────────────────────────────────
    /// Deteccao centralizada de MPPM (Multiplayer Play Mode) no Unity 6.
    ///
    ///  ▸ IsClone: true quando este Editor e um Virtual Player
    ///  ▸ CloneId: identificador estavel do clone (vpId, 8+ chars)
    ///  ▸ Deteccao via command-line args: --virtual-project-clone, -vpId=
    ///  ▸ Fallback para env var UNITY_MULTIPLAYER_PLAY_MODE_PLAYER_INDEX
    /// ─────────────────────────────────────────────────────────
    /// </summary>
    public static class MppmHelper
    {
        private static bool _initialized;
        private static bool _isClone;
        private static string _cloneId = "";

        /// <summary>True se este Editor é um clone MPPM (Virtual Player).</summary>
        public static bool IsClone
        {
            get { if (!_initialized) Detect(); return _isClone; }
        }

        /// <summary>
        /// Identificador unico do clone. Vazio se nao for clone.
        /// Estavel entre sessoes (baseado no vpId do MPPM).
        /// </summary>
        public static string CloneId
        {
            get { if (!_initialized) Detect(); return _cloneId; }
        }

        private static void Detect()
        {
            _initialized = true;
            _isClone = false;
            _cloneId = "";

#if UNITY_EDITOR
            // Metodo 1: command-line args do MPPM v1.6+ (Unity 6)
            // Clones recebem --virtual-project-clone e -vpId={id}
            var args = System.Environment.GetCommandLineArgs();
            bool cloneFlag = false;
            string vpId = "";

            foreach (string arg in args)
            {
                if (arg == "--virtual-project-clone")
                    cloneFlag = true;
                else if (arg.StartsWith("-vpId="))
                    vpId = arg.Substring(6);
            }

            if (cloneFlag)
            {
                _isClone = true;
                _cloneId = !string.IsNullOrEmpty(vpId)
                    ? vpId
                    : System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
                Debug.Log($"[MppmHelper] Clone MPPM detectado. vpId: {_cloneId}");
                return;
            }
#endif

            // Metodo 2: env var (versoes anteriores ou configuracoes customizadas)
            string envVar = System.Environment.GetEnvironmentVariable(
                "UNITY_MULTIPLAYER_PLAY_MODE_PLAYER_INDEX");
            if (!string.IsNullOrEmpty(envVar))
            {
                _isClone = true;
                _cloneId = envVar;
                Debug.Log($"[MppmHelper] MPPM detectado via env var: #{envVar}");
            }
        }
    }
}
