using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Auth;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// Configura o prefab do jogador conforme propriedade de rede (IsOwner).
    /// Owner: controles habilitados (jogador local).
    /// Nao-owner: controles desabilitados (jogador remoto — posicao via ClientNetworkTransform).
    /// </summary>
    public class PlayerNetworkSetup : NetworkBehaviour
    {
        [Header("Componentes de Input (desabilitados para jogadores remotos)")]
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private MonoBehaviour cameraController;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private MonoBehaviour playerShooting;
        [SerializeField] private MeleeCombatSystem meleeCombat;
        [SerializeField] private MonoBehaviour playerCombatManager;
        [SerializeField] private LocalPlayerInputBridge localInputBridge;

        [Header("Objetos exclusivos do jogador local")]
        [SerializeField] private GameObject[] localOnlyObjects;

        public override void OnNetworkSpawn()
        {
            // Auto-fallback: resolve any SerializedFields left unassigned in the Inspector.
            // Needed because "Samurai Variant" prefab has all fields at {fileID: 0}.
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (cameraController == null) cameraController = GetComponent<CameraController>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (playerShooting == null) playerShooting = GetComponent<PlayerShooting>();
            if (meleeCombat == null) meleeCombat = GetComponent<MeleeCombatSystem>();
            if (playerCombatManager == null) playerCombatManager = GetComponent<PlayerCombatManager>();
            if (localInputBridge == null) localInputBridge = GetComponent<LocalPlayerInputBridge>();

            if (IsOwner)
                SetupAsLocalPlayer();
            else
                SetupAsRemotePlayer();
        }

        private void SetupAsLocalPlayer()
        {
            Debug.Log($"[PlayerNetworkSetup] Jogador LOCAL inicializado | ClientId: {OwnerClientId}");
            StartCoroutine(RegisterIdentityWithBridgeWhenReady());
            StartCoroutine(FinishLocalSetupNextFrame());
        }

        /// <summary>
        /// Executa no frame seguinte ao OnNetworkSpawn para garantir que Start() de todos os
        /// MonoBehaviours do prefab (ex: PlayerMovement) já tenha rodado antes de sobrescrever
        /// o estado do cursor e das torres.
        /// </summary>
        private IEnumerator FinishLocalSetupNextFrame()
        {
            // Aguarda um frame — Start() é sempre chamado após OnNetworkSpawn em objetos
            // spawnados dinamicamente pelo NGO.
            yield return null;

            // ── 0. PlayerInput — garantir ActionMap "Player" ativa ───────────────────
            // Disable→Enable cycle forces fresh Keyboard&Mouse pairing.
            // When multiple player prefabs instantiate on the same machine, the first
            // PlayerInput.Awake() grabs the device. If the local avatar spawns second,
            // it fails to pair. By this coroutine frame all remote PlayerInputs are
            // already disabled (SetupAsRemotePlayer runs sync in OnNetworkSpawn),
            // so re-enabling here succeeds on the fresh OnEnable pairing attempt.
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = false;
                yield return null; // one extra frame — lets Input System fully release remote devices
                playerInput.enabled = true;
                playerInput.SwitchCurrentActionMap("Player");
            }

            if (localInputBridge == null)
                localInputBridge = GetComponent<LocalPlayerInputBridge>();

            if (localInputBridge == null)
                localInputBridge = gameObject.AddComponent<LocalPlayerInputBridge>();

            localInputBridge.enabled = true;

            Debug.Log("[PlayerNetworkSetup] PlayerInput configurado no ActionMap 'Player'.");

            // ── 1. Cursor ────────────────────────────────────────────────────────────
            // PlayerMovement.Start() trava o cursor apenas se o tutorial "PLAYER_MOVEMENT"
            // estiver concluído. Em multiplayer, clientes remotos podem não ter completado
            // o tutorial na sua máquina — aplicamos o lock diretamente aqui.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            Debug.Log("[PlayerNetworkSetup] Cursor travado para jogador local (multiplayer bypass).");

            // ── 2. Torres disponíveis ─────────────────────────────────────────────────
            // GameSetupManager.SpawnPlayerServerSide chama SetAvailableTowers apenas no
            // servidor (host). Clientes não-host nunca recebem essa chamada, então suas
            // torres não aparecem no BuildUI. Chamamos aqui com os dados locais do cliente.
            if (BuildManager.Instance != null && GameDataManager.Instance != null)
            {
                BuildManager.Instance.SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
                Debug.Log("[PlayerNetworkSetup] Torres disponíveis configuradas para jogador local.");
            }
            else
            {
                Debug.LogWarning(
                    "[PlayerNetworkSetup] Não foi possível configurar torres: " +
                    $"BuildManager={BuildManager.Instance != null} | " +
                    $"GameDataManager={GameDataManager.Instance != null}");
            }
        }

        /// <summary>
        /// Envia productUserId + sessionToken do EOS para o servidor via PlayerIdentityBridge.
        /// Permite que o servidor saiba qual jogador EOS corresponde a este clientId NGO.
        ///
        /// Retry: NGO nao garante ordem de OnNetworkSpawn entre objetos da cena, entao o
        /// PlayerIdentityBridge pode ainda nao ter sido spawnado quando este jogador inicia.
        /// Aguarda ate 5s pelo bridge aparecer. Se falhar, loga erro.
        /// </summary>
        private IEnumerator RegisterIdentityWithBridgeWhenReady()
        {
            const float timeoutSeconds = 5f;
            float elapsed = 0f;

            while (PlayerIdentityBridge.Instance?.NetworkObject?.IsSpawned != true)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= timeoutSeconds)
                {
                    Debug.LogError(
                        "[PlayerNetworkSetup] Timeout aguardando PlayerIdentityBridge spawnar. " +
                        "Verifique se o NetworkObject esta presente em CenaMapaTeste. " +
                        "Identidade EOS nao sera vinculada ao clientId.");
                    yield break;
                }
                yield return null;
            }

            string userId = EOSAuthenticator.Instance?.CurrentProductUserId ?? "";
            string token  = SessionManager.Instance?.sessionToken ?? "";

            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogWarning("[PlayerNetworkSetup] userId EOS vazio — EOS pode ainda nao ter concluido auth. Identity nao registrada.");
                yield break;
            }

            PlayerIdentityBridge.Instance.RegisterPlayerServerRpc(userId, token);
        }

        private void SetupAsRemotePlayer()
        {
            Debug.Log($"[PlayerNetworkSetup] Jogador REMOTO inicializado | ClientId: {OwnerClientId}");

            // Desabilita PlayerInput para evitar conflito de device-pairing entre instâncias no Unity 6 Input System
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;

            if (localInputBridge == null) localInputBridge = GetComponent<LocalPlayerInputBridge>();
            if (localInputBridge != null) localInputBridge.enabled = false;

            if (cameraController != null) cameraController.enabled = false;

            // Servidor precisa manter a lógica de gameplay viva para validar tiros,
            // cooldowns e demais RPCs enviados por jogadores possuídos por outros clientes.
            if (!IsServer)
            {
                if (playerShooting != null) playerShooting.enabled = false;
                if (meleeCombat != null) meleeCombat.enabled = false;
                if (playerCombatManager != null) playerCombatManager.enabled = false;
            }

            if (localOnlyObjects != null)
            {
                foreach (var obj in localOnlyObjects)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }
        }

        public void EnableMovement()
        {
            if (!IsOwner) return;
            if (movement != null) movement.enabled = true;
        }

        public void DisableMovement()
        {
            if (!IsOwner) return;
            if (movement != null) movement.enabled = false;
        }
    }
}
