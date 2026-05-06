using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Managers.Loading
{
    /// <summary>
    /// Escuta os eventos de troca de cena do Netcode for GameObjects (NGO) e gerencia 
    /// a aparição e o desaparecimento da tela de Loading automaticamente.
    /// Deve ser anexado no mesmo objeto do LoadingScreenUI.
    /// </summary>
    public class SceneTransitionHandler : MonoBehaviour
    {
        private static SceneTransitionHandler instance;
        private const string HIDE_MSG = "HideLoadingScreen";

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                // O DontDestroyOnLoad já é feito no LoadingScreenUI, mas não machuca garantir.
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Aguardamos o NetworkManager estar totalmente vivo para conectar os eventos
            StartCoroutine(WaitForNetworkManager());
        }

        private System.Collections.IEnumerator WaitForNetworkManager()
        {
            while (NetworkManager.Singleton == null)
            {
                yield return null;
            }

            NetworkManager.Singleton.OnServerStarted += SubscribeToSceneEvents;
            NetworkManager.Singleton.OnClientStarted += SubscribeToSceneEvents;
            
            // Se o jogo já tiver iniciado (o NetworkManager já subiu)
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
            {
                SubscribeToSceneEvents();
            }
        }

        private void SubscribeToSceneEvents()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                // Previne inscrições duplicadas
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
                NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
            }

            // Registrar para ouvir a mensagem de "pode apagar a tela" (funciona para clientes e servidor)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(HIDE_MSG);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(HIDE_MSG, OnReceiveHideMessage);
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= SubscribeToSceneEvents;
                NetworkManager.Singleton.OnClientStarted -= SubscribeToSceneEvents;
                
                if (NetworkManager.Singleton.CustomMessagingManager != null)
                    NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(HIDE_MSG);
            }
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                // Início do carregamento da cena (Disparado para todo mundo)
                case SceneEventType.Load:
                    if (LoadingScreenUI.Instance != null)
                    {
                        LoadingScreenUI.Instance.Show();
                    }
                    break;

                // Disparado no Servidor/Host quando TODOS os clientes terminarem de baixar/carregar a cena!
                // É o "Sinal Verde" oficial do multiplayer.
                case SceneEventType.LoadEventCompleted:
                    if (NetworkManager.Singleton.IsServer)
                    {
                        HideLoadingForEveryone();
                    }
                    break;
            }
        }

        private void HideLoadingForEveryone()
        {
            // Manda um ping sem payload via CustomMessagingManager para a rede inteira desligar a tela.
            // (Assim evitamos criar um NetworkBehaviour só pra desligar a tela)
            using FastBufferWriter writer = new FastBufferWriter(0, Unity.Collections.Allocator.Temp);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(HIDE_MSG, writer);
            
            // O Host esconde para si mesmo imediatamente
            if (LoadingScreenUI.Instance != null)
            {
                LoadingScreenUI.Instance.Hide();
            }
        }

        // Chamado nos clientes quando recebem a mensagem do Host
        private void OnReceiveHideMessage(ulong senderClientId, FastBufferReader messagePayload)
        {
            // Checagem de segurança, só aceita comando de desligar do Servidor (ClientId 0)
            if (senderClientId == NetworkManager.ServerClientId)
            {
                if (LoadingScreenUI.Instance != null)
                {
                    LoadingScreenUI.Instance.Hide();
                }
            }
        }
    }
}
