using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.GameServer
{
    /// <summary>
    /// Gerencia o estado da partida no servidor
    /// Controla inicio, pausas, fim da partida
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        private static MatchManager _instance;
        public static MatchManager Instance => _instance;

        [Header("Match Settings")]
        [SerializeField] private float matchStartDelay = 3f;
        [SerializeField] private bool autoStartMatch = false;

        // Estado da partida
        public NetworkVariable<MatchState> CurrentMatchState = new NetworkVariable<MatchState>(
            MatchState.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> CurrentWave = new NetworkVariable<int>(0);
        public NetworkVariable<float> MatchTime = new NetworkVariable<float>(0f);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                InitializeMatch();
            }

            // Escutar mudancas de estado
            CurrentMatchState.OnValueChanged += OnMatchStateChanged;
        }

        private void InitializeMatch()
        {
            Debug.Log("[MatchManager] Inicializando partida...");
            CurrentMatchState.Value = MatchState.WaitingForPlayers;

            if (autoStartMatch)
            {
                Invoke(nameof(StartMatch), matchStartDelay);
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            // Atualizar tempo de partida se estiver jogando
            if (CurrentMatchState.Value == MatchState.Playing)
            {
                MatchTime.Value += Time.deltaTime;
            }
        }

        /// <summary>
        /// Iniciar a partida (apenas servidor)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void StartMatchServerRpc()
        {
            if (!IsServer) return;

            StartMatch();
        }

        private void StartMatch()
        {
            Debug.Log("[MatchManager] Iniciando partida!");
            CurrentMatchState.Value = MatchState.Starting;

            // Notificar clientes
            OnMatchStartingClientRpc();

            // Aguardar delay e comecar
            Invoke(nameof(BeginPlaying), matchStartDelay);
        }

        private void BeginPlaying()
        {
            CurrentMatchState.Value = MatchState.Playing;
            MatchTime.Value = 0f;
            CurrentWave.Value = 1;

            Debug.Log("[MatchManager] Partida em andamento!");
        }

        /// <summary>
        /// Pausar a partida
        /// </summary>
        public void PauseMatch()
        {
            if (!IsServer) return;

            Debug.Log("[MatchManager] Partida pausada");
            CurrentMatchState.Value = MatchState.Paused;
        }

        /// <summary>
        /// Retomar a partida
        /// </summary>
        public void ResumeMatch()
        {
            if (!IsServer) return;

            Debug.Log("[MatchManager] Partida retomada");
            CurrentMatchState.Value = MatchState.Playing;
        }

        /// <summary>
        /// Encerrar a partida com vitoria
        /// </summary>
        public void EndMatchVictory()
        {
            if (!IsServer) return;

            Debug.Log("[MatchManager] Partida terminada - VITORIA!");
            CurrentMatchState.Value = MatchState.Victory;
            OnMatchEndedClientRpc(true);
        }

        /// <summary>
        /// Encerrar a partida com derrota
        /// </summary>
        public void EndMatchDefeat()
        {
            if (!IsServer) return;

            Debug.Log("[MatchManager] Partida terminada - DERROTA!");
            CurrentMatchState.Value = MatchState.Defeat;
            OnMatchEndedClientRpc(false);
        }

        // Client RPCs
        [ClientRpc]
        private void OnMatchStartingClientRpc()
        {
            Debug.Log("[MatchManager] Partida iniciando em breve...");
            // TODO: Mostrar contagem regressiva
        }

        [ClientRpc]
        private void OnMatchEndedClientRpc(bool victory)
        {
            Debug.Log($"[MatchManager] Partida encerrada - {(victory ? "VITORIA" : "DERROTA")}");
            // TODO: Mostrar tela de resultado
        }

        // Event Handler
        private void OnMatchStateChanged(MatchState oldState, MatchState newState)
        {
            Debug.Log($"[MatchManager] Estado mudou: {oldState} -> {newState}");
            // TODO: Disparar eventos para outros sistemas reagirem
        }

        private void OnDestroy()
        {
            CurrentMatchState.OnValueChanged -= OnMatchStateChanged;
        }
    }

    /// <summary>
    /// Estados possiveis da partida
    /// </summary>
    public enum MatchState
    {
        WaitingForPlayers,  // Aguardando jogadores conectarem
        Starting,           // Contagem regressiva para inicio
        Playing,            // Partida em andamento
        Paused,             // Partida pausada
        Victory,            // Jogadores venceram
        Defeat,             // Jogadores perderam
        Ended               // Partida encerrada
    }
}
