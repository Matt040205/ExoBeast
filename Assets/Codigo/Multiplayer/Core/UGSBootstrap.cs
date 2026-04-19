using UnityEngine;
using System.Collections;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// Inicializa Unity Gaming Services (UGS) com auth anonima ao iniciar.
    /// Necessario antes de qualquer chamada ao RelayService (builds nao-editor).
    /// Colocar como componente em um GameObject na cena de Lobby/Menu.
    /// </summary>
    public class UGSBootstrap : MonoBehaviour
    {
        public static UGSBootstrap Instance { get; private set; }
        public bool IsReady { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(InitCoroutine());
        }

        private IEnumerator InitCoroutine()
        {
            var task = UnityServices.InitializeAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                Debug.LogWarning("[UGSBootstrap] Falha ao inicializar UGS: " + task.Exception?.GetBaseException().Message);
                yield break;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                var authTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
                yield return new WaitUntil(() => authTask.IsCompleted);

                if (authTask.IsFaulted)
                {
                    Debug.LogWarning("[UGSBootstrap] Falha na auth UGS: " + authTask.Exception?.GetBaseException().Message);
                    yield break;
                }
            }

            IsReady = true;
            Debug.Log("[UGSBootstrap] UGS pronto. PlayerId=" + AuthenticationService.Instance.PlayerId);
        }
    }
}
