#if !EOS_DISABLE && UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

using UnityEngine;
using PlayEveryWare.EpicOnlineServices;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// ── WindowsPlatformSpecifics ─────────────────────────
    /// Implementacao de PlatformSpecifics para Windows/Editor, exigida pelo PlayEveryWare EOS.
    ///
    ///  ▸ Registrado via [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]
    ///  ▸ GetTempDir(): subdiretorio isolado por clone MPPM (via MppmHelper)
    ///  ▸ ConfigureSystemPlatformCreateOptions(): desabilita RTC (voz)
    ///  ▸ Sem esta classe, EOS SDK falha ao inicializar no Windows standalone e Editor
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class WindowsPlatformSpecifics : PlatformSpecifics<WindowsConfig>
    {
        public WindowsPlatformSpecifics() : base(PlatformManager.Platform.Windows)
        {
        }

        public override string GetTempDir()
        {
            // Clones MPPM precisam de cache isolado — caso contrário,
            // todos compartilham a mesma credencial Device ID do EOS.
            if (MppmHelper.IsClone)
            {
                string dir = System.IO.Path.Combine(
                    Application.temporaryCachePath, $"eos_clone_{MppmHelper.CloneId}");
                System.IO.Directory.CreateDirectory(dir);
                Debug.Log($"[WindowsPlatformSpecifics] MPPM clone — EOS CacheDir: {dir}");
                return dir;
            }

            return Application.temporaryCachePath;
        }

        public override void ConfigureSystemPlatformCreateOptions(ref EOSCreateOptions createOptions)
        {
            // Setting RTCOptions to null disables RTC features
            createOptions.options.RTCOptions = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var instance = new WindowsPlatformSpecifics();
            EOSManagerPlatformSpecificsSingleton.SetEOSManagerPlatformSpecificsInterface(instance);

            string tempDir = instance.GetTempDir();
            Debug.Log($"[WindowsPlatformSpecifics] Inicializado. EOS CacheDir: {tempDir}");
        }
    }
}

#endif
