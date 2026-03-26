using UnityEngine;
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

        [Header("Objetos exclusivos do jogador local")]
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
            RegisterIdentityWithBridge();
        }

        /// <summary>
        /// Envia productUserId + sessionToken do EOS para o servidor via PlayerIdentityBridge.
        /// Permite que o servidor saiba qual jogador EOS corresponde a este clientId NGO.
        /// </summary>
        private void RegisterIdentityWithBridge()
        {
            if (PlayerIdentityBridge.Instance == null)
            {
                Debug.LogWarning("[PlayerNetworkSetup] PlayerIdentityBridge nao encontrado na cena.");
                return;
            }

            string userId = "";
            string token = "";

            if (EOSAuthenticator.Instance != null)
                userId = EOSAuthenticator.Instance.CurrentProductUserId ?? "";

            if (SessionManager.Instance != null)
                token = SessionManager.Instance.sessionToken ?? "";

            PlayerIdentityBridge.Instance.RegisterPlayerServerRpc(userId, token);
        }

        private void SetupAsRemotePlayer()
        {
            Debug.Log($"[PlayerNetworkSetup] Jogador REMOTO inicializado | ClientId: {OwnerClientId}");

            if (movement != null) movement.enabled = false;
            if (cameraController != null) cameraController.enabled = false;
            if (characterController != null) characterController.enabled = false;
            if (playerShooting != null) playerShooting.enabled = false;
            if (meleeCombat != null) meleeCombat.enabled = false;
            if (playerCombatManager != null) playerCombatManager.enabled = false;

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
