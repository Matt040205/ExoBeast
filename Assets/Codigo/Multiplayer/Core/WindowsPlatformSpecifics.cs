/*
 * Workaround para inicializar o PlatformSpecifics no Windows
 * O PlayEveryWare EOS não tem uma implementação específica para Windows standalone
 * Esta classe garante que o singleton seja inicializado corretamente
 */

#if !EOS_DISABLE && UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

using UnityEngine;
using PlayEveryWare.EpicOnlineServices;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// Implementação de PlatformSpecifics para Windows
    /// Esta classe é necessária porque o PlayEveryWare EOS não fornece uma por padrão
    /// </summary>
    public class WindowsPlatformSpecifics : PlatformSpecifics<WindowsConfig>
    {
        public WindowsPlatformSpecifics() : base(PlatformManager.Platform.Windows)
        {
        }

        /// <summary>
        /// Configura as opções específicas da plataforma para criação do EOS Platform
        /// </summary>
        public override void ConfigureSystemPlatformCreateOptions(ref EOSCreateOptions createOptions)
        {
            // Não chamar base.ConfigureSystemPlatformCreateOptions() para evitar criar RTCOptions vazio

            // Desabilitar RTC por enquanto (voz não é necessária para autenticação básica)
            // Setting RTCOptions to null disables RTC features
            createOptions.options.RTCOptions = null;
        }

        // Método estático para registrar a instância no singleton
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Registrar a instância de WindowsPlatformSpecifics no singleton
            var instance = new WindowsPlatformSpecifics();
            EOSManagerPlatformSpecificsSingleton.SetEOSManagerPlatformSpecificsInterface(instance);

            Debug.Log("[WindowsPlatformSpecifics] Platform specifics inicializado para Windows");
        }
    }
}

#endif
