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
            Debug.Log($"[MatchManager] OnNetworkSpawn! IsServer={IsServer}, IsClient={IsClient}, MatchTime={MatchTime.Value:F1}s, State={CurrentMatchState.Value}");

            if (IsServer)
            {
                InitializeMatch();
            }

            CurrentMatchState.OnValueChanged += OnMatchStateChanged;

            // ── Sync imediato do timer para TODOS os clientes ──
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ForceTimerSync(MatchTime.Value);
            }
            else
            {
                Debug.LogWarning("[MatchManager] UIManager.Instance é null no OnNetworkSpawn! Timer será sincronizado quando UIManager aparecer.");
            }
        }

        private void InitializeMatch()
        {
            CurrentMatchState.Value = MatchState.WaitingForPlayers;

            if (autoStartMatch)
            {
                Invoke(nameof(StartMatch), matchStartDelay);
            }
        }

        // Acumulador local do tempo de partida; só escreve em MatchTime.Value a cada 1s
        // para evitar 60 NetworkVariable updates/segundo (timer de UI tem precisao de seg).
        private float _matchTimeAccumulator = 0f;
        private const float MATCH_TIME_PUBLISH_INTERVAL = 1f;

#if UNITY_EDITOR
        private float _debugLogTimer = 0f;
#endif

        private void Update()
        {
            if (!IsServer) return;

#if UNITY_EDITOR
            // Log de diagnostico apenas no editor — em build polui o Player.log e aloca strings.
            _debugLogTimer += Time.deltaTime;
            if (_debugLogTimer >= 5f)
            {
                _debugLogTimer = 0f;
                Debug.Log($"[MatchManager] Update - State={CurrentMatchState.Value}, MatchTime={MatchTime.Value:F1}s, autoStartMatch={autoStartMatch}");
            }
#endif

            if (CurrentMatchState.Value == MatchState.Playing)
            {
                _matchTimeAccumulator += Time.deltaTime;
                if (_matchTimeAccumulator >= MATCH_TIME_PUBLISH_INTERVAL)
                {
                    MatchTime.Value += _matchTimeAccumulator;
                    _matchTimeAccumulator = 0f;
                }
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
            // OPTIMIZATION (Sprint 4 / Item E6 - 2026-05-21): chamada do ClientRpc stub removida.
            // Clientes reagem a CurrentMatchState.OnValueChanged (subscrito em OnNetworkSpawn).
            CurrentMatchState.Value = MatchState.Starting;

            Invoke(nameof(BeginPlaying), matchStartDelay);
        }

        private void BeginPlaying()
        {
            CurrentMatchState.Value = MatchState.Playing;
            MatchTime.Value = 0f;
            _matchTimeAccumulator = 0f;
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
        }

        public void EndMatchDefeat()
        {
            if (!IsServer) return;
            CurrentMatchState.Value = MatchState.Defeat;
        }

        // OPTIMIZATION (Sprint 4 / Item E6 - 2026-05-21): 2 ClientRpc stubs removidos
        // (OnMatchStartingClientRpc / OnMatchEndedClientRpc).
        // Antes: 3 ClientRpcs vazios por partida (start + end victory/defeat) - pacotes
        // inuteis no Network Profiler (~96 bytes por partida desperdicados em 4-player).
        // Agora: clientes reagem a CurrentMatchState.OnValueChanged (assinatura existente).
        // Sem isso: ruido em logs do servidor + bytes/s falsos no profiling de rede.
        // Para reintroduzir countdown/SFX no futuro, assinar OnValueChanged em vez de criar ClientRpc:
        //   CurrentMatchState.OnValueChanged += (oldState, newState) => { if (newState == MatchState.Starting) ShowCountdown(); }
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
