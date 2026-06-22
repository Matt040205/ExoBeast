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
                float[] backoff = { 1f, 2f, 4f };
                bool signed = false;

                for (int attempt = 0; attempt < backoff.Length; attempt++)
                {
                    if (attempt > 0)
                    {
                        Debug.LogWarning($"[UGSBootstrap] Retry auth UGS ({attempt}/{backoff.Length - 1}) em {backoff[attempt - 1]}s...");
                        yield return new WaitForSeconds(backoff[attempt - 1]);
                    }

                    var authTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
                    yield return new WaitUntil(() => authTask.IsCompleted);

                    if (!authTask.IsFaulted)
                    {
                        signed = true;
                        break;
                    }

                    Debug.LogWarning("[UGSBootstrap] Falha na auth UGS (tentativa " + (attempt + 1) + "): " +
                                     authTask.Exception?.GetBaseException().Message);
                }

                if (!signed)
                {
                    Debug.LogError("[UGSBootstrap] Auth UGS falhou apos todas as tentativas. Relay indisponivel.");
                    yield break;
                }
            }

            IsReady = true;
            Debug.Log("[UGSBootstrap] UGS pronto. PlayerId=" + AuthenticationService.Instance.PlayerId);
        }
    }
}
