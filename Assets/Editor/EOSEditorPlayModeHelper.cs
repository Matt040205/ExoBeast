#if UNITY_EDITOR
using UnityEditor;
using PlayEveryWare.EpicOnlineServices;

namespace ExoBeasts.Multiplayer.Editor
{
    /// <summary>
    /// ── EOSEditorPlayModeHelper ─────────────────────────────
    /// Garante que o EOS SDK nativo seja descarregado antes de
    /// cada domain reload no Editor.
    ///
    /// Problema: EOSSDK-Win64-Shipping.dll persiste entre sessoes
    /// de Play Mode. Como Application.quitting nao dispara ao
    /// sair do Play Mode, as threads nativas do EOS continuam
    /// rodando. Na segunda entrada, Unity fica esperando essas
    /// threads terminarem → "busy for 14:41" hang.
    ///
    /// Solucao: AssemblyReloadEvents.beforeAssemblyReload dispara
    /// antes de cada reload de dominio. Chamamos OnApplicationShutdown()
    /// que executa UnloadAllLibraries() e encerra as threads nativas.
    /// ─────────────────────────────────────────────────────────
    /// </summary>
    [InitializeOnLoad]
    public static class EOSEditorPlayModeHelper
    {
        static EOSEditorPlayModeHelper()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            if (EOSManager.Instance != null)
            {
                UnityEngine.Debug.Log("[EOSEditorPlayModeHelper] Encerrando EOS antes do domain reload...");
                EOSManager.Instance.OnApplicationShutdown();
                UnityEngine.Debug.Log("[EOSEditorPlayModeHelper] EOS encerrado com sucesso.");
            }
        }
    }
}
#endif
