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

        // C4 audit: guards anti-double-fire do OnEOSInitialized.
        // _initializationInProgress impede que uma segunda chamada a Initialize()
        // rode enquanto a coroutine WaitForPlayEveryWareInit ainda esta ativa.
        // _initializationFired impede que o evento seja invocado mais de uma vez
        // mesmo se dois caminhos chegarem ao ponto de dispatch.
        private bool _initializationInProgress = false;
        private bool _initializationFired = false;

        public bool IsInitialized => isInitialized;
        public bool IsConnected => isConnected;

        public event Action OnEOSInitialized;
        public event Action OnEOSShutdown;
        public event Action<string> OnInitializationFailed;

#if !EOS_DISABLE
        private PlatformInterface platformInterface;

        public PlatformInterface GetPlatformInterface()
        {
            // Tenta o EOSManager vivo primeiro; caso tenha sido destruido na troca de cena,
            // usa o cache local obtido no momento da inicializacao.
            if (PlayEveryWare.EpicOnlineServices.EOSManager.Instance != null)
            {
                var live = PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface();
                if (live != null) return live;
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

#if !EOS_DISABLE
        private void Update()
        {
            // Fallback tick: garante que o EOS SDK processa callbacks mesmo se o
            // PlayEveryWare EOSManager nao estiver presente ou ativo na cena.
            // Double-tick e seguro — callbacks sao removidos da fila apos disparar.
            if (isInitialized)
                GetPlatformInterface()?.Tick();
        }
#endif

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

            // C4 audit: impede double-entry enquanto a coroutine de espera ainda esta ativa.
            // Sem este guard, chamar Initialize() duas vezes antes da coroutine terminar
            // dispararia OnEOSInitialized em dobro quando ambas as coroutines resolvessem.
            if (_initializationInProgress)
            {
                Debug.LogWarning("[EOSManagerWrapper] Initialize() ja em progresso, ignorando chamada duplicada");
                return;
            }
            _initializationInProgress = true;

#if !EOS_DISABLE
            Debug.Log("[EOSManagerWrapper] Iniciando inicializacao do EOS SDK...");

            if (eosConfig == null)
            {
                // A7 audit: antes, null era silenciosamente aceito e a inicializacao continuava
                // com credenciais vazias, causando falhas opacas downstream.
                string error = "EOSConfig nao encontrado em Resources/EOSConfig_Main. Crie o asset ou atribua via Inspector.";
                Debug.LogError($"[EOSManagerWrapper] {error}");
                _initializationInProgress = false;
                OnInitializationFailed?.Invoke(error);
                return;
            }

            eosConfig.LoadCredentialsFromFile();

            if (!eosConfig.ValidateCredentials())
            {
                string error = "Credenciais EOS invalidas ou incompletas";
                Debug.LogError($"[EOSManagerWrapper] {error}");
                _initializationInProgress = false;
                OnInitializationFailed?.Invoke(error);
                return;
            }

            ApplyCredentialsToPlayEveryWare();

            // Busca o MonoBehaviour do EOSManager INCLUINDO componentes desabilitados.
            // Motivo: o PlayEveryWare desabilita a si mesmo em Awake() quando detecta
            // que ja existe uma s_EOSManagerInstance (comum entre reloads de play mode
            // no editor). FindObjectOfType() sem flags ignora componentes desabilitados
            // desde Unity 2020.1, causando falso "nao encontrado".
            var eosMonos = UnityEngine.Object.FindObjectsByType<PlayEveryWare.EpicOnlineServices.EOSManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var eosMono = eosMonos.Length > 0 ? eosMonos[0] : null;

            if (eosMono != null)
                DontDestroyOnLoad(eosMono.gameObject);

            // Tenta obter a platform interface — funciona mesmo se o MonoBehaviour estiver
            // desabilitado, porque EOSManager.Instance (EOSSingleton) mantem o estado estatico.
            // Nosso proprio Update() faz o Tick() de fallback se o MonoBehaviour nao estiver
            // rodando, entao nao precisamos forcar um MonoBehaviour ativo.
            var platform = PlayEveryWare.EpicOnlineServices.EOSManager.Instance?.GetEOSPlatformInterface();

            if (platform != null)
            {
                platformInterface = platform;   // cache — usado se o EOSManager for destruido
                isInitialized = true;
                Debug.Log($"[EOSManagerWrapper] EOS SDK inicializado com sucesso! (MonoBehaviour presente: {eosMono != null}, ativo: {(eosMono != null ? eosMono.enabled : false)})");
                FireInitializedOnce();
            }
            else if (eosMono != null)
            {
                // MonoBehaviour existe mas platform ainda nao foi inicializada — aguarda.
                Debug.Log("[EOSManagerWrapper] Aguardando PlayEveryWare EOSManager inicializar...");
                StartCoroutine(WaitForPlayEveryWareInit());
            }
            else
            {
                Debug.LogError("[EOSManagerWrapper] PlayEveryWare EOSManager nao encontrado na cena! Adicione o prefab EOSManager.");
                _initializationInProgress = false;
                OnInitializationFailed?.Invoke("PlayEveryWare EOSManager nao encontrado na cena");
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
                var platform = PlayEveryWare.EpicOnlineServices.EOSManager.Instance?.GetEOSPlatformInterface();
                if (platform != null)
                {
                    var eosMonos2 = UnityEngine.Object.FindObjectsByType<PlayEveryWare.EpicOnlineServices.EOSManager>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    if (eosMonos2.Length > 0) DontDestroyOnLoad(eosMonos2[0].gameObject);
                    platformInterface = platform;   // cache
                    isInitialized = true;
                    Debug.Log("[EOSManagerWrapper] EOS SDK inicializado com sucesso!");
                    FireInitializedOnce();
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogError("[EOSManagerWrapper] Timeout aguardando inicializacao do EOS");
            _initializationInProgress = false;
            OnInitializationFailed?.Invoke("Timeout na inicializacao");
        }

        // C4 audit: dispatch central do evento OnEOSInitialized, com guard anti-double-fire.
        // Chamado tanto pelo caminho sincrono (Initialize) quanto pelo caminho assincrono
        // (WaitForPlayEveryWareInit). Reset do flag acontece em Shutdown() para permitir
        // reinicializacao limpa (ex.: logout e login novo).
        private void FireInitializedOnce()
        {
            if (_initializationFired)
            {
                Debug.LogWarning("[EOSManagerWrapper] OnEOSInitialized ja foi disparado anteriormente; ignorando segundo dispatch.");
                return;
            }
            _initializationFired = true;
            _initializationInProgress = false;
            OnEOSInitialized?.Invoke();
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
            // C4 audit: reset dos flags para permitir reinicializacao limpa apos Shutdown.
            _initializationFired = false;
            _initializationInProgress = false;
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
