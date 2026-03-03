using UnityEngine;
using UnityEngine.UI;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Auth;

#if !EOS_DISABLE
using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
#endif

namespace ExoBeasts.Multiplayer.Testing
{
    /// <summary>
    /// Script de teste para autenticacao EOS via Device ID
    /// Este script cria automaticamente o EOSManager do PlayEveryWare se necessario
    /// </summary>
    public class EOSAuthTest : MonoBehaviour
    {
        [Header("Configuracao")]
        [SerializeField] private string playerDisplayName = "TestPlayer";

        [Header("UI (Opcional)")]
        [SerializeField] private Text statusText;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button logoutButton;

        [Header("Estado")]
        [SerializeField] private bool autoLoginOnStart = true;
        [SerializeField] private bool isLoggedIn = false;
        [SerializeField] private string currentUserId = "";

#if !EOS_DISABLE
        private ProductUserId localProductUserId;
#endif

        private void Awake()
        {
            // Verificar se o EOSManager existe
#if !EOS_DISABLE
            var existingManager = FindObjectOfType<PlayEveryWare.EpicOnlineServices.EOSManager>();
            if (existingManager == null)
            {
                Debug.LogError("[EOSAuthTest] ERRO CRÍTICO: EOSManager não encontrado na cena!");
                Debug.LogError("[EOSAuthTest] Você precisa adicionar um GameObject com o componente EOSManager na cena.");
                Debug.LogError("[EOSAuthTest] Vá em GameObject > Create Empty, renomeie para 'EOSManager' e adicione o componente PlayEveryWare.EpicOnlineServices.EOSManager");
            }
            else
            {
                Debug.Log($"[EOSAuthTest] EOSManager encontrado: {existingManager.name}");
            }
#endif
        }

        private void Start()
        {
            // Configurar botoes se existirem
            if (loginButton != null)
            {
                loginButton.onClick.AddListener(DoLogin);
            }
            if (logoutButton != null)
            {
                logoutButton.onClick.AddListener(DoLogout);
                logoutButton.interactable = false;
            }

            UpdateStatus("Aguardando EOS SDK...");

            // Aguardar inicializacao do EOS
            if (autoLoginOnStart)
            {
                StartCoroutine(WaitForEOSAndLogin());
            }
        }

        /// <summary>
        /// Garante que o EOSManager do PlayEveryWare existe na cena
        /// </summary>
        private void EnsurePlayEveryWareEOSManager()
        {
#if !EOS_DISABLE
            // Verificar se ja existe um EOSManager na cena
            var existingManager = FindObjectOfType<PlayEveryWare.EpicOnlineServices.EOSManager>();
            if (existingManager == null)
            {
                Debug.Log("[EOSAuthTest] Criando PlayEveryWare EOSManager...");
                GameObject eosManagerGO = new GameObject("EOSManager");
                eosManagerGO.AddComponent<PlayEveryWare.EpicOnlineServices.EOSManager>();
            }
            else
            {
                Debug.Log("[EOSAuthTest] PlayEveryWare EOSManager ja existe na cena");
            }
#endif
        }

        private System.Collections.IEnumerator WaitForEOSAndLogin()
        {
#if !EOS_DISABLE
            UpdateStatus("Aguardando inicializacao do EOS...");

            float timeout = 15f;
            float elapsed = 0f;

            // Aguardar o EOSManager inicializar
            while (elapsed < timeout)
            {
                if (PlayEveryWare.EpicOnlineServices.EOSManager.Instance != null)
                {
                    var platform = PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface();
                    if (platform != null)
                    {
                        Debug.Log("[EOSAuthTest] EOS SDK inicializado!");
                        Debug.Log($"[EOSAuthTest] Platform válido: {platform != null}");

                        var connectInterface = platform.GetConnectInterface();
                        Debug.Log($"[EOSAuthTest] ConnectInterface válido: {connectInterface != null}");

                        if (connectInterface != null)
                        {
                            UpdateStatus("EOS SDK pronto! Fazendo login...");
                            // Fazer login via Device ID
                            yield return StartCoroutine(LoginWithDeviceId());
                            yield break;
                        }
                        else
                        {
                            Debug.LogWarning("[EOSAuthTest] Platform inicializado mas ConnectInterface é null. Aguardando mais um frame...");
                        }
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogError("[EOSAuthTest] Timeout aguardando EOS SDK");
            UpdateStatus("ERRO: Timeout - Verifique a configuracao do EOS");
#else
            UpdateStatus("EOS desabilitado (EOS_DISABLE)");
            yield break;
#endif
        }

#if !EOS_DISABLE
        private System.Collections.IEnumerator LoginWithDeviceId()
        {
            var platform = PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface();
            var connectInterface = platform?.GetConnectInterface();

            if (connectInterface == null)
            {
                UpdateStatus("ERRO: ConnectInterface nao disponivel");
                yield break;
            }

            // Passo 1: Criar Device ID
            UpdateStatus("Criando Device ID...");

            bool deviceIdCreated = false;
            Result deviceIdResult = Result.UnexpectedError;

            var createDeviceIdOptions = new CreateDeviceIdOptions
            {
                DeviceModel = $"{SystemInfo.deviceModel}_{SystemInfo.deviceName}"
            };

            connectInterface.CreateDeviceId(ref createDeviceIdOptions, null, (ref CreateDeviceIdCallbackInfo data) =>
            {
                deviceIdResult = data.ResultCode;
                deviceIdCreated = true;
            });

            // Aguardar callback
            while (!deviceIdCreated)
            {
                yield return null;
            }

            if (deviceIdResult != Result.Success && deviceIdResult != Result.DuplicateNotAllowed)
            {
                UpdateStatus($"ERRO ao criar Device ID: {deviceIdResult}");
                yield break;
            }

            Debug.Log($"[EOSAuthTest] Device ID: {(deviceIdResult == Result.DuplicateNotAllowed ? "ja existe" : "criado")}");

            // Passo 2: Login com Device ID
            UpdateStatus("Fazendo login...");

            bool loginCompleted = false;
            Result loginResult = Result.UnexpectedError;
            ProductUserId resultUserId = null;
            ContinuanceToken continuanceToken = null;

            var credentials = new Credentials
            {
                Type = ExternalCredentialType.DeviceidAccessToken,
                Token = null
            };

            var userLoginInfo = new UserLoginInfo
            {
                DisplayName = playerDisplayName
            };

            var loginOptions = new LoginOptions
            {
                Credentials = credentials,
                UserLoginInfo = userLoginInfo
            };

            connectInterface.Login(ref loginOptions, null, (ref LoginCallbackInfo data) =>
            {
                loginResult = data.ResultCode;
                resultUserId = data.LocalUserId;
                continuanceToken = data.ContinuanceToken;
                loginCompleted = true;
            });

            // Aguardar callback
            while (!loginCompleted)
            {
                yield return null;
            }

            if (loginResult == Result.Success)
            {
                OnLoginSuccess(resultUserId);
                yield break;
            }
            else if (loginResult == Result.InvalidUser)
            {
                // Usuario nao existe, criar
                Debug.Log("[EOSAuthTest] Usuario nao existe, criando...");
                UpdateStatus("Criando usuario...");

                yield return StartCoroutine(CreateUser(connectInterface, continuanceToken));
            }
            else
            {
                UpdateStatus($"ERRO no login: {loginResult}");
            }
        }

        private System.Collections.IEnumerator CreateUser(ConnectInterface connectInterface, ContinuanceToken continuanceToken)
        {
            bool createCompleted = false;
            Result createResult = Result.UnexpectedError;
            ProductUserId resultUserId = null;

            var createUserOptions = new CreateUserOptions
            {
                ContinuanceToken = continuanceToken
            };

            connectInterface.CreateUser(ref createUserOptions, null, (ref CreateUserCallbackInfo data) =>
            {
                createResult = data.ResultCode;
                resultUserId = data.LocalUserId;
                createCompleted = true;
            });

            // Aguardar callback
            while (!createCompleted)
            {
                yield return null;
            }

            if (createResult == Result.Success)
            {
                OnLoginSuccess(resultUserId);
            }
            else
            {
                UpdateStatus($"ERRO ao criar usuario: {createResult}");
            }
        }

        private void OnLoginSuccess(ProductUserId userId)
        {
            localProductUserId = userId;
            currentUserId = userId.ToString();
            isLoggedIn = true;

            Debug.Log($"[EOSAuthTest] Login bem-sucedido! ProductUserId: {currentUserId}");
            UpdateStatus($"Logado!\nID: {currentUserId}");

            // Atualizar SessionManager
            SessionManager.Instance.StartSession(currentUserId, playerDisplayName);

            if (loginButton != null) loginButton.interactable = false;
            if (logoutButton != null) logoutButton.interactable = true;
        }
#endif

        public void DoLogin()
        {
#if !EOS_DISABLE
            if (!isLoggedIn)
            {
                StartCoroutine(LoginWithDeviceId());
            }
#endif
        }

        public void DoLogout()
        {
#if !EOS_DISABLE
            if (isLoggedIn)
            {
                isLoggedIn = false;
                currentUserId = "";
                localProductUserId = null;

                SessionManager.Instance.EndSession();
                UpdateStatus("Desconectado");

                if (loginButton != null) loginButton.interactable = true;
                if (logoutButton != null) logoutButton.interactable = false;

                Debug.Log("[EOSAuthTest] Logout realizado");
            }
#endif
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[EOSAuthTest] Status: {message}");
        }

        // Debug GUI para testes rapidos sem UI
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 350, 250));
            GUILayout.BeginVertical("box");

            GUILayout.Label("=== EOS Auth Test ===");

#if !EOS_DISABLE
            bool eosReady = PlayEveryWare.EpicOnlineServices.EOSManager.Instance != null &&
                           PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface() != null;
            GUILayout.Label($"EOS SDK Pronto: {eosReady}");
#else
            GUILayout.Label("EOS SDK: DESABILITADO");
#endif

            GUILayout.Label($"Logado: {isLoggedIn}");

            if (isLoggedIn)
            {
                GUILayout.Label($"Usuario: {playerDisplayName}");
                GUILayout.Label($"ID: {currentUserId}");
            }

            GUILayout.Space(10);

            if (!isLoggedIn)
            {
                if (GUILayout.Button("Login com Device ID"))
                {
                    DoLogin();
                }
            }
            else
            {
                if (GUILayout.Button("Logout"))
                {
                    DoLogout();
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Verifique o Console para logs detalhados");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
