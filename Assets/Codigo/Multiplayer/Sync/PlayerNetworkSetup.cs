using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// Ativa e desativa componentes de input por papel de rede.
    /// Owner: controles habilitados (jogador local).
    /// Nao-owner: controles desabilitados (jogador remoto — posicao via ClientNetworkTransform).
    /// Adicionar ao prefab do jogador junto com ClientNetworkTransform e NetworkAnimator.
    /// </summary>
    public class PlayerNetworkSetup : NetworkBehaviour
    {
        [Header("Controles do Jogador")]
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private MonoBehaviour cameraController;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private MonoBehaviour playerShooting;
        [SerializeField] private MeleeCombatSystem meleeCombat;
        [SerializeField] private MonoBehaviour playerCombatManager;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
                DisableLocalControls();
        }

        private void DisableLocalControls()
        {
            if (movement != null) movement.enabled = false;
            if (cameraController != null) cameraController.enabled = false;
            if (characterController != null) characterController.enabled = false;
            if (playerShooting != null) playerShooting.enabled = false;
            if (meleeCombat != null) meleeCombat.enabled = false;
            if (playerCombatManager != null) playerCombatManager.enabled = false;
        }
    }
}
