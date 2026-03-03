using UnityEngine;
using System;
using ExoBeasts.Multiplayer.Core;

#if !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Auth;
#endif

namespace ExoBeasts.Multiplayer.Auth
{
    /// <summary>
    /// Gerencia autenticacao no EOS via Device ID
    /// Device ID permite login anonimo sem necessidade de conta Epic
    /// Ideal para desenvolvimento e testes
    /// </summary>
    public class EOSAuthenticator : MonoBehaviour
    {
        private static EOSAuthenticator _instance;
        public static EOSAuthenticator Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("EOSAuthenticator");
                    _instance = go.AddComponent<EOSAuthenticator>();
                }
                return _instance;
            }
        }

        [Header("Estado")]
        [SerializeField] private bool isLoggedIn = false;
        [SerializeField] private string currentProductUserId = "";
        [SerializeField] private string deviceIdName = "ExoBeastsPlayer";

        public bool IsLoggedIn => isLoggedIn;
        public string CurrentProductUserId => currentProductUserId;

        // Eventos
        public event Action<string> OnLoginSuccess;
        public event Action<string> OnLoginFailed;
        public event Action OnLogout;

#if !EOS_DISABLE
        private ProductUserId localProductUserId;
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

        /// <summary>
        /// Login via Device ID (anonimo)
        /// Cria um Device ID unico se nao existir, depois faz login
        /// </summary>
        public void LoginWithDeviceId()
        {
#if !EOS_DISABLE
            if (isLoggedIn)
            {
                Debug.LogWarning("[EOSAuthenticator] Ja esta logado");
                return;
            }

            var eosManager = EOSManagerWrapper.Instance;
            if (!eosManager.IsInitialized)
            {
                Debug.LogError("[EOSAuthenticator] EOS nao inicializado. Chame EOSManagerWrapper.Initialize() primeiro");
                OnLoginFailed?.Invoke("EOS nao inicializado");
                return;
            }

            var connectInterface = eosManager.GetConnectInterface();
            if (connectInterface == null)
            {
                Debug.LogError("[EOSAuthenticator] ConnectInterface nao disponivel");
                OnLoginFailed?.Invoke("ConnectInterface nao disponivel");
                return;
            }

            Debug.Log("[EOSAuthenticator] Iniciando login via Device ID...");

            // Primeiro, criar Device ID se necessario
            CreateDeviceIdAndLogin(connectInterface);
#else
            Debug.LogWarning("[EOSAuthenticator] EOS desabilitado");
            OnLoginFailed?.Invoke("EOS desabilitado");
#endif
        }

#if !EOS_DISABLE
        private void CreateDeviceIdAndLogin(ConnectInterface connectInterface)
        {
            var createDeviceIdOptions = new CreateDeviceIdOptions
            {
                DeviceModel = $"{SystemInfo.deviceModel}_{SystemInfo.deviceName}"
            };

            Debug.Log($"[EOSAuthenticator] Criando/recuperando Device ID: {createDeviceIdOptions.DeviceModel}");

            connectInterface.CreateDeviceId(ref createDeviceIdOptions, null, OnCreateDeviceIdComplete);
        }

        private void OnCreateDeviceIdComplete(ref CreateDeviceIdCallbackInfo data)
        {
            // DuplicateNotAllowed significa que o Device ID ja existe - isso e OK
            if (data.ResultCode == Result.Success || data.ResultCode == Result.DuplicateNotAllowed)
            {
                if (data.ResultCode == Result.DuplicateNotAllowed)
                {
                    Debug.Log("[EOSAuthenticator] Device ID ja existe, usando existente");
                }
                else
                {
                    Debug.Log("[EOSAuthenticator] Device ID criado com sucesso");
                }

                // Agora fazer login com o Device ID
                PerformDeviceIdLogin();
            }
            else
            {
                Debug.LogError($"[EOSAuthenticator] Falha ao criar Device ID: {data.ResultCode}");
                OnLoginFailed?.Invoke($"Falha ao criar Device ID: {data.ResultCode}");
            }
        }

        private void PerformDeviceIdLogin()
        {
            var eosManager = EOSManagerWrapper.Instance;
            var connectInterface = eosManager.GetConnectInterface();

            if (connectInterface == null)
            {
                OnLoginFailed?.Invoke("ConnectInterface nao disponivel");
                return;
            }

            var credentials = new Epic.OnlineServices.Connect.Credentials
            {
                Type = ExternalCredentialType.DeviceidAccessToken,
                Token = null // Device ID nao precisa de token
            };

            var userLoginInfo = new UserLoginInfo
            {
                DisplayName = deviceIdName
            };

            var loginOptions = new Epic.OnlineServices.Connect.LoginOptions
            {
                Credentials = credentials,
                UserLoginInfo = userLoginInfo
            };

            Debug.Log("[EOSAuthenticator] Realizando login via Device ID...");
            connectInterface.Login(ref loginOptions, null, OnConnectLoginComplete);
        }

        private void OnConnectLoginComplete(ref Epic.OnlineServices.Connect.LoginCallbackInfo data)
        {
            if (data.ResultCode == Result.Success)
            {
                localProductUserId = data.LocalUserId;
                currentProductUserId = localProductUserId.ToString();
                isLoggedIn = true;

                Debug.Log($"[EOSAuthenticator] Login bem-sucedido! ProductUserId: {currentProductUserId}");

                // Atualizar EOSManagerWrapper
                EOSManagerWrapper.Instance.SetConnected(true);

                // Atualizar SessionManager
                SessionManager.Instance.StartSession(currentProductUserId, deviceIdName);

                OnLoginSuccess?.Invoke(currentProductUserId);
            }
            else if (data.ResultCode == Result.InvalidUser)
            {
                // Usuario nao existe, precisamos criar
                Debug.Log("[EOSAuthenticator] Usuario nao existe, criando novo usuario...");
                CreateUser(data.ContinuanceToken);
            }
            else
            {
                Debug.LogError($"[EOSAuthenticator] Falha no login: {data.ResultCode}");
                OnLoginFailed?.Invoke($"Falha no login: {data.ResultCode}");
            }
        }

        private void CreateUser(ContinuanceToken continuanceToken)
        {
            var eosManager = EOSManagerWrapper.Instance;
            var connectInterface = eosManager.GetConnectInterface();

            if (connectInterface == null)
            {
                OnLoginFailed?.Invoke("ConnectInterface nao disponivel para criar usuario");
                return;
            }

            var createUserOptions = new CreateUserOptions
            {
                ContinuanceToken = continuanceToken
            };

            Debug.Log("[EOSAuthenticator] Criando novo usuario EOS...");
            connectInterface.CreateUser(ref createUserOptions, null, OnCreateUserComplete);
        }

        private void OnCreateUserComplete(ref CreateUserCallbackInfo data)
        {
            if (data.ResultCode == Result.Success)
            {
                localProductUserId = data.LocalUserId;
                currentProductUserId = localProductUserId.ToString();
                isLoggedIn = true;

                Debug.Log($"[EOSAuthenticator] Usuario criado e logado! ProductUserId: {currentProductUserId}");

                // Atualizar EOSManagerWrapper
                EOSManagerWrapper.Instance.SetConnected(true);

                // Atualizar SessionManager
                SessionManager.Instance.StartSession(currentProductUserId, deviceIdName);

                OnLoginSuccess?.Invoke(currentProductUserId);
            }
            else
            {
                Debug.LogError($"[EOSAuthenticator] Falha ao criar usuario: {data.ResultCode}");
                OnLoginFailed?.Invoke($"Falha ao criar usuario: {data.ResultCode}");
            }
        }
#endif

        /// <summary>
        /// Logout do EOS
        /// </summary>
        public void Logout()
        {
#if !EOS_DISABLE
            if (!isLoggedIn)
            {
                Debug.LogWarning("[EOSAuthenticator] Nao esta logado");
                return;
            }

            Debug.Log("[EOSAuthenticator] Realizando logout...");

            // Limpar estado local
            isLoggedIn = false;
            currentProductUserId = "";
            localProductUserId = null;

            // Atualizar EOSManagerWrapper
            EOSManagerWrapper.Instance.SetConnected(false);

            // Encerrar sessao
            SessionManager.Instance.EndSession();

            Debug.Log("[EOSAuthenticator] Logout realizado");
            OnLogout?.Invoke();
#endif
        }

        /// <summary>
        /// Definir nome de exibicao para Device ID
        /// Deve ser chamado antes do login
        /// </summary>
        public void SetDeviceIdName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                deviceIdName = name;
            }
        }

#if !EOS_DISABLE
        /// <summary>
        /// Obter o ProductUserId atual
        /// </summary>
        public ProductUserId GetProductUserId()
        {
            return localProductUserId;
        }
#endif
    }
}
