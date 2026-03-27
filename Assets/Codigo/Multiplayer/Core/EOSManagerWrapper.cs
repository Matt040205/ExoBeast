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
    /// ── EOSManagerWrapper ────────────────────────────────
    /// Wrapper sobre o PlayEveryWare EOSManager — ponto central de acesso ao EOS SDK.
    ///
    ///  ▸ Initialize(): aguarda init do PlayEveryWare via coroutine (timeout 10s)
    ///  ▸ GetConnectInterface() / GetAuthInterface(): acessores tipados
    ///  ▸ OnEOSInitialized: evento disparado quando SDK esta pronto
    ///  ▸ SetConnected(bool): atualizado pelo EOSAuthenticator apos login
    ///  ▸ Singleton com DontDestroyOnLoad; Start() chama Initialize() automaticamente
    /// ─────────────────────────────────────────────────────
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

        public event Action OnEOSInitialized;
        public event Action OnEOSShutdown;
        public event Action<string> OnInitializationFailed;

#if !EOS_DISABLE
        private PlatformInterface platformInterface;

        public PlatformInterface GetPlatformInterface()
        {
            if (PlayEveryWare.EpicOnlineServices.EOSManager.Instance != null)
            {
                return PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface();
            }
            return platformInterface;
        }

        public ConnectInterface GetConnectInterface()
        {
            var platform = GetPlatformInterface();
            return platform?.GetConnectInterface();
        }

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
            if (eosConfig == null)
            {
                eosConfig = Resources.Load<EOSConfig>("EOSConfig_Main");
                if (eosConfig == null)
                {
                    Debug.LogWarning("[EOSManagerWrapper] EOSConfig nao encontrado. Atribua via Inspector ou crie em Resources/EOSConfig_Main");
                }
            }

            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized)
            {
                Debug.LogWarning("[EOSManagerWrapper] EOS ja esta inicializado");
                return;
            }

#if !EOS_DISABLE
            Debug.Log("[EOSManagerWrapper] Iniciando inicializacao do EOS SDK...");

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

                ApplyCredentialsToPlayEveryWare();
            }

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

        // O PlayEveryWare usa seu proprio sistema de config em StreamingAssets
        private void ApplyCredentialsToPlayEveryWare()
        {
            if (eosConfig == null) return;
            Debug.Log("[EOSManagerWrapper] Credenciais carregadas do arquivo externo");
        }
#endif

        public void Shutdown()
        {
            if (!isInitialized) return;

#if !EOS_DISABLE
            Debug.Log("[EOSManagerWrapper] Desligando EOS SDK...");

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

        public void SetConnected(bool connected)
        {
            isConnected = connected;
        }
    }
}
