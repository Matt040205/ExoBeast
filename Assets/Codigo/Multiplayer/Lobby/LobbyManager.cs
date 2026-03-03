using UnityEngine;
using System;
using System.Collections.Generic;

namespace ExoBeasts.Multiplayer.Lobby
{
    /// <summary>
    /// Gerencia operacoes de lobby (criar, buscar, entrar, sair)
    /// Interface com Epic Lobby Service
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        private static LobbyManager _instance;
        public static LobbyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("LobbyManager");
                    _instance = go.AddComponent<LobbyManager>();
                }
                return _instance;
            }
        }

        // Eventos
        public event Action<LobbyInfo> OnLobbyCreated;
        public event Action<List<LobbyInfo>> OnLobbiesFound;
        public event Action<LobbyInfo> OnLobbyJoined;
        public event Action OnLobbyLeft;
        public event Action<LobbyMember> OnMemberJoined;
        public event Action<LobbyMember> OnMemberLeft;
        public event Action<LobbyMember> OnMemberUpdated;
        public event Action<string> OnError;

        [Header("Current Lobby")]
        private LobbyInfo currentLobby;
        private List<LobbyMember> currentMembers = new List<LobbyMember>();
        private bool isInLobby = false;

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
        /// Criar um novo lobby
        /// </summary>
        public void CreateLobby(LobbySettings settings)
        {
            Debug.Log($"[LobbyManager] Criando lobby: {settings.lobbyName}");

            // TODO: Implementar criacao de lobby via EOS
            // 1. Obter Lobby Interface do EOSManager
            // 2. Criar CreateLobbyOptions com settings
            // 3. Chamar LobbyInterface.CreateLobby()
            // 4. No callback, armazenar lobby ID e disparar OnLobbyCreated

            // Simulacao por enquanto
            currentLobby = new LobbyInfo
            {
                lobbyId = Guid.NewGuid().ToString(),
                lobbyName = settings.lobbyName,
                hostDisplayName = "Host",
                currentPlayers = 1,
                maxPlayers = settings.maxPlayers,
                mapName = settings.mapName,
                isPublic = settings.isPublic,
                state = LobbyState.WaitingForPlayers
            };

            isInLobby = true;
            OnLobbyCreated?.Invoke(currentLobby);
            Debug.Log($"[LobbyManager] Lobby criado: {currentLobby.lobbyId}");
        }

        /// <summary>
        /// Buscar lobbies disponiveis
        /// </summary>
        public void SearchLobbies(LobbySearchFilter filter)
        {
            Debug.Log("[LobbyManager] Buscando lobbies...");

            // TODO: Implementar busca de lobbies via EOS
            // 1. Criar LobbySearchOptions com filtros
            // 2. Adicionar filtros (nome, publico/privado, etc)
            // 3. Chamar LobbyInterface.CreateLobbySearch()
            // 4. Executar busca e processar resultados

            // Simulacao por enquanto
            List<LobbyInfo> lobbies = new List<LobbyInfo>();
            OnLobbiesFound?.Invoke(lobbies);
            Debug.Log($"[LobbyManager] Encontrados {lobbies.Count} lobbies");
        }

        /// <summary>
        /// Entrar em um lobby existente
        /// </summary>
        public void JoinLobby(string lobbyId)
        {
            Debug.Log($"[LobbyManager] Entrando no lobby: {lobbyId}");

            // TODO: Implementar entrada em lobby via EOS
            // 1. Criar JoinLobbyOptions com lobby ID
            // 2. Chamar LobbyInterface.JoinLobby()
            // 3. No callback, armazenar dados do lobby

            Debug.LogWarning("[LobbyManager] JoinLobby ainda nao implementado");
        }

        /// <summary>
        /// Sair do lobby atual
        /// </summary>
        public void LeaveLobby()
        {
            if (!isInLobby)
            {
                Debug.LogWarning("[LobbyManager] Nao esta em um lobby");
                return;
            }

            Debug.Log($"[LobbyManager] Saindo do lobby: {currentLobby.lobbyId}");

            // TODO: Implementar saida de lobby via EOS
            // LobbyInterface.LeaveLobby()

            currentLobby = null;
            currentMembers.Clear();
            isInLobby = false;
            OnLobbyLeft?.Invoke();
        }

        /// <summary>
        /// Definir um atributo do membro (ex: personagem selecionado)
        /// </summary>
        public void SetMemberAttribute(string key, string value)
        {
            Debug.Log($"[LobbyManager] Definindo atributo: {key} = {value}");

            // TODO: Implementar via EOS
            // LobbyInterface.UpdateLobbyMember()
        }

        /// <summary>
        /// Marcar jogador como pronto
        /// </summary>
        public void SetReady(bool ready)
        {
            Debug.Log($"[LobbyManager] Marcando como pronto: {ready}");
            SetMemberAttribute(MemberAttributes.IS_READY, ready.ToString());
        }

        /// <summary>
        /// Selecionar personagem
        /// </summary>
        public void SelectCharacter(int characterIndex)
        {
            Debug.Log($"[LobbyManager] Selecionando personagem: {characterIndex}");
            SetMemberAttribute(MemberAttributes.CHARACTER_INDEX, characterIndex.ToString());
        }

        /// <summary>
        /// Iniciar partida (apenas host)
        /// </summary>
        public void StartMatch()
        {
            Debug.Log("[LobbyManager] Iniciando partida...");

            // TODO: Implementar inicio de partida
            // 1. Verificar se e host
            // 2. Verificar se todos estao prontos
            // 3. Solicitar servidor ao EGSH
            // 4. Enviar IP:Porta para todos os membros
            // 5. Transicionar para cena de jogo

            Debug.LogWarning("[LobbyManager] StartMatch ainda nao implementado");
        }

        // Getters
        public bool IsInLobby() => isInLobby;
        public LobbyInfo GetCurrentLobby() => currentLobby;
        public List<LobbyMember> GetCurrentMembers() => currentMembers;
    }
}
