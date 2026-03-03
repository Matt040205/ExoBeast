using UnityEngine;
using System;

#if !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Auth;
using PlayEveryWare.EpicOnlineServices;
#endif

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// Wrapper para gerenciamento do EOS SDK
    /// Integra nosso sistema de credenciais com o PlayEveryWare EOS Plugin
    /// </summary>
    public class EOSManagerWrapper : MonoBehaviour
    {
        private static EOSManagerWrapper _instance;
        public static EOSManagerWrapper Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("EOSManagerWrapper");
                    _instance = go.AddComponent<EOSManagerWrapper>();
                }
                return _instance;
            }
        }

        [Header("Configuracao")]
        [SerializeField] private EOSConfig eosConfig;

        [Header("Estado")]
        [SerializeField] private bool isInitialized = false;
        [SerializeField] private bool isConnected = false;

        public bool IsInitialized => isInitialized;
        public bool IsConnected => isConnected;

        // Eventos
        public event Action OnEOSInitialized;
        public event Action OnEOSShutdown;
        public event Action<string> OnInitializationFailed;

#if !EOS_DISABLE
        private PlatformInterface platformInterface;

        /// <summary>
        /// Obter a interface da plataforma EOS
        /// </summary>
        public PlatformInterface GetPlatformInterface()
        {
            if (PlayEveryWare.EpicOnlineServices.EOSManager.Instance != null)
            {
                return PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface();
            }
            return platformInterface;
        }

        /// <summary>
        /// Obter a interface Connect para autenticacao
        /// </summary>
        public ConnectInterface GetConnectInterface()
        {
            var platform = GetPlatformInterface();
            return platform?.GetConnectInterface();
        }

        /// <summary>
        /// Obter a interface Auth para login Epic Account
        /// </summary>
        public AuthInterface GetAuthInterface()
        {
            var platform = GetPlatformInterface();
            return platform?.GetAuthInterface();
        }
#endif

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Tentar encontrar EOSConfig se nao foi atribuido
            if (eosConfig == null)
            {
                eosConfig = Resources.Load<EOSConfig>("EOSConfig_Main");
                if (eosConfig == null)
                {
                    Debug.LogWarning("[EOSManagerWrapper] EOSConfig nao encontrado. Atribua via Inspector ou crie em Resources/EOSConfig_Main");
                }
            }
        }

        /// <summary>
        /// Inicializar o EOS SDK
        /// Carrega credenciais e configura o PlayEveryWare
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                Debug.LogWarning("[EOSManagerWrapper] EOS ja esta inicializado");
                return;
            }

#if !EOS_DISABLE
            Debug.Log("[EOSManagerWrapper] Iniciando inicializacao do EOS SDK...");

            // Carregar credenciais do arquivo externo
            if (eosConfig != null)
            {
                eosConfig.LoadCredentialsFromFile();

                if (!eosConfig.ValidateCredentials())
                {
                    string error = "Credenciais EOS invalidas ou incompletas";
                    Debug.LogError($"[EOSManagerWrapper] {error}");
                    OnInitializationFailed?.Invoke(error);
                    return;
                }

                // Aplicar credenciais ao sistema PlayEveryWare
                ApplyCredentialsToPlayEveryWare();
            }

            // O PlayEveryWare EOSManager inicializa automaticamente
            // Verificar se ja esta pronto
            if (PlayEveryWare.EpicOnlineServices.EOSManager.Instance != null)
            {
                var platform = PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface();
                if (platform != null)
                {
                    isInitialized = true;
                    Debug.Log("[EOSManagerWrapper] EOS SDK inicializado com sucesso!");
                    OnEOSInitialized?.Invoke();
                }
                else
                {
                    Debug.Log("[EOSManagerWrapper] Aguardando PlayEveryWare EOSManager inicializar...");
                    // PlayEveryWare inicializa no Awake/Start, entao deve estar pronto em breve
                    StartCoroutine(WaitForPlayEveryWareInit());
                }
            }
            else
            {
                Debug.LogError("[EOSManagerWrapper] PlayEveryWare EOSManager nao encontrado na cena!");
                OnInitializationFailed?.Invoke("PlayEveryWare EOSManager nao encontrado");
            }
#else
            Debug.LogWarning("[EOSManagerWrapper] EOS esta desabilitado (EOS_DISABLE definido)");
#endif
        }

#if !EOS_DISABLE
        private System.Collections.IEnumerator WaitForPlayEveryWareInit()
        {
            float timeout = 10f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (PlayEveryWare.EpicOnlineServices.EOSManager.Instance != null)
                {
                    var platform = PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface();
                    if (platform != null)
                    {
                        isInitialized = true;
                        Debug.Log("[EOSManagerWrapper] EOS SDK inicializado com sucesso!");
                        OnEOSInitialized?.Invoke();
                        yield break;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogError("[EOSManagerWrapper] Timeout aguardando inicializacao do EOS");
            OnInitializationFailed?.Invoke("Timeout na inicializacao");
        }

        /// <summary>
        /// Aplicar credenciais carregadas ao sistema PlayEveryWare
        /// O PlayEveryWare usa seu proprio sistema de config em StreamingAssets
        /// Esta funcao configura em runtime quando necessario
        /// </summary>
        private void ApplyCredentialsToPlayEveryWare()
        {
            if (eosConfig == null) return;

            Debug.Log("[EOSManagerWrapper] Credenciais carregadas do arquivo externo");
            // O PlayEveryWare le configs de StreamingAssets/EOS/
            // Para sobrescrever em runtime, usamos o sistema de Config do PlayEveryWare

            // Nota: O PlayEveryWare gerencia isso internamente
            // Nossas credenciais sao usadas como fallback/validacao
        }
#endif

        /// <summary>
        /// Desligar o EOS SDK
        /// </summary>
        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

#if !EOS_DISABLE
            Debug.Log("[EOSManagerWrapper] Desligando EOS SDK...");

            // Limpar credenciais da memoria
            if (eosConfig != null)
            {
                eosConfig.ClearCredentials();
            }

            isInitialized = false;
            isConnected = false;
            OnEOSShutdown?.Invoke();

            Debug.Log("[EOSManagerWrapper] EOS SDK desligado");
#endif
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

#if !EOS_DISABLE
        private void Update()
        {
            // O PlayEveryWare EOSManager ja faz o Tick automaticamente
            // Nao precisamos chamar manualmente
        }
#endif

        /// <summary>
        /// Marcar como conectado (chamado pelo EOSAuthenticator apos login)
        /// </summary>
        public void SetConnected(bool connected)
        {
            isConnected = connected;
        }
    }
}
