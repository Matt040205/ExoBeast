using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// ── PlayerNetworkSetup ───────────────────────────────
    /// Configura o prefab do jogador conforme propriedade de rede (IsOwner).
    ///
    ///  ▸ Jogador LOCAL (IsOwner): componentes habilitados normalmente
    ///  ▸ Jogador REMOTO: desabilita input, camera, HUD e CharacterController
    ///  ▸ CharacterController DEVE ser desabilitado no remoto (conflita com ClientNetworkTransform)
    ///  ▸ EnableMovement / DisableMovement: helpers para cutscenes, morte, etc.
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class PlayerNetworkSetup : NetworkBehaviour
    {
        [Header("Componentes de Input (desabilitados para jogadores remotos)")]
        [Tooltip("Componente que le input e move o personagem")]
        [SerializeField] private PlayerMovement movement;

        [Tooltip("Componente que controla a camera (Mouse Look)")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("Sistema de tiro ranged")]
        [SerializeField] private PlayerShooting shooting;

        [Tooltip("Sistema de combate corpo a corpo")]
        [SerializeField] private MeleeCombatSystem melee;

        [Tooltip("Orquestrador de combate do jogador")]
        [SerializeField] private PlayerCombatManager combat;

        [Header("CharacterController")]
        [Tooltip("O CharacterController DEVE ser desabilitado no jogador remoto.\n" +
                 "O ClientNetworkTransform precisa setar a posicao diretamente.\n" +
                 "Deixe este campo vazio e o script busca automaticamente.")]
        [SerializeField] private CharacterController characterController;

        [Header("Objetos exclusivos do jogador local")]
        [Tooltip("GameObjects a desativar para jogadores remotos.\n" +
                 "Adicione aqui: CinemachineCamera, AudioListener, HUD Canvas,\n" +
                 "qualquer efeito visual/audio que seja so do jogador local.")]
        [SerializeField] private GameObject[] localOnlyObjects;

        public override void OnNetworkSpawn()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (IsOwner)
                SetupAsLocalPlayer();
            else
                SetupAsRemotePlayer();
        }

        private void SetupAsLocalPlayer()
        {
            Debug.Log($"[PlayerNetworkSetup] Jogador LOCAL inicializado | ClientId: {OwnerClientId}");
        }

        private void SetupAsRemotePlayer()
        {
            Debug.Log($"[PlayerNetworkSetup] Jogador REMOTO inicializado | ClientId: {OwnerClientId}");

            if (movement != null)       movement.enabled       = false;
            if (cameraController != null) cameraController.enabled = false;
            if (shooting != null)       shooting.enabled       = false;
            if (melee != null)          melee.enabled          = false;
            if (combat != null)         combat.enabled         = false;

            if (characterController != null) characterController.enabled = false;

            foreach (var obj in localOnlyObjects)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        /// <summary>
        /// Habilita temporariamente o movimento (ex: cutscene acabou).
        /// Apenas funciona no jogador local.
        /// </summary>
        public void EnableMovement()
        {
            if (!IsOwner) return;
            if (movement != null) movement.enabled = true;
        }

        /// <summary>
        /// Desabilita temporariamente o movimento (ex: durante cutscene, morte).
        /// Apenas funciona no jogador local.
        /// </summary>
        public void DisableMovement()
        {
            if (!IsOwner) return;
            if (movement != null) movement.enabled = false;
        }
    }
}
