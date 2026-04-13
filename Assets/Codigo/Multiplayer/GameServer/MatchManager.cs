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
            CurrentMatchState.Value = MatchState.Starting;

            OnMatchStartingClientRpc();

            Invoke(nameof(BeginPlaying), matchStartDelay);
        }

        private void BeginPlaying()
        {
            CurrentMatchState.Value = MatchState.Playing;
            MatchTime.Value = 0f;
            CurrentWave.Value = 1;
        }

        public void PauseMatch()
        {
            if (!IsServer) return;
            CurrentMatchState.Value = MatchState.Paused;
        }

        public void ResumeMatch()
        {
            if (!IsServer) return;
            CurrentMatchState.Value = MatchState.Playing;
        }

        public void EndMatchVictory()
        {
            if (!IsServer) return;
            CurrentMatchState.Value = MatchState.Victory;
            OnMatchEndedClientRpc(true);
        }

        public void EndMatchDefeat()
        {
            if (!IsServer) return;
            CurrentMatchState.Value = MatchState.Defeat;
            OnMatchEndedClientRpc(false);
        }

        [ClientRpc]
        private void OnMatchStartingClientRpc()
        {
            // Stub: adicionar countdown visual ou SFX aqui
        }

        [ClientRpc]
        private void OnMatchEndedClientRpc(bool victory)
        {
            // Stub: adicionar efeito visual/sonoro de fim de partida aqui
        }

        private void OnMatchStateChanged(MatchState oldState, MatchState newState) { }

        public override void OnNetworkDespawn()
        {
            CurrentMatchState.OnValueChanged -= OnMatchStateChanged;
            base.OnNetworkDespawn();
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
