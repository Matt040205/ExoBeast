using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.GameServer
{
    /// <summary>
    /// ── MatchManager ─────────────────────────────────────
    /// Controla o estado da partida em rede (servidor-autoritativo).
    ///
    ///  ▸ NetworkVariable CurrentMatchState: WaitingForPlayers → Starting → Playing → Victory/Defeat
    ///  ▸ MatchTime acumulado no servidor enquanto Playing
    ///  ▸ StartMatchServerRpc: chamavel de qualquer cliente
    ///  ▸ EndMatchVictory / EndMatchDefeat: encerram e notificam via ClientRpc
    ///  ▸ Singleton
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        private static MatchManager _instance;
        public static MatchManager Instance => _instance;

        [Header("Match Settings")]
        [SerializeField] private float matchStartDelay = 3f;
        [SerializeField] private bool autoStartMatch = false;

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

            if (CurrentMatchState.Value == MatchState.Playing)
            {
                MatchTime.Value += Time.deltaTime;
            }
        }

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

            OnMatchStartingClientRpc();

            Invoke(nameof(BeginPlaying), matchStartDelay);
        }

        private void BeginPlaying()
        {
            CurrentMatchState.Value = MatchState.Playing;
            MatchTime.Value = 0f;
            CurrentWave.Value = 1;

            Debug.Log("[MatchManager] Partida em andamento!");
        }

        public void PauseMatch()
        {
            if (!IsServer) return;

            Debug.Log("[MatchManager] Partida pausada");
            CurrentMatchState.Value = MatchState.Paused;
        }

        public void ResumeMatch()
        {
            if (!IsServer) return;

            Debug.Log("[MatchManager] Partida retomada");
            CurrentMatchState.Value = MatchState.Playing;
        }

        public void EndMatchVictory()
        {
            if (!IsServer) return;

            Debug.Log("[MatchManager] Partida terminada - VITORIA!");
            CurrentMatchState.Value = MatchState.Victory;
            OnMatchEndedClientRpc(true);
        }

        public void EndMatchDefeat()
        {
            if (!IsServer) return;

            Debug.Log("[MatchManager] Partida terminada - DERROTA!");
            CurrentMatchState.Value = MatchState.Defeat;
            OnMatchEndedClientRpc(false);
        }

        [ClientRpc]
        private void OnMatchStartingClientRpc()
        {
            Debug.Log("[MatchManager] Partida iniciando em breve...");
        }

        [ClientRpc]
        private void OnMatchEndedClientRpc(bool victory)
        {
            Debug.Log($"[MatchManager] Partida encerrada - {(victory ? "VITORIA" : "DERROTA")}");
        }

        private void OnMatchStateChanged(MatchState oldState, MatchState newState)
        {
            Debug.Log($"[MatchManager] Estado mudou: {oldState} -> {newState}");
        }

        private void OnDestroy()
        {
            CurrentMatchState.OnValueChanged -= OnMatchStateChanged;
        }
    }

    public enum MatchState
    {
        WaitingForPlayers,
        Starting,
        Playing,
        Paused,
        Victory,
        Defeat,
        Ended
    }
}
