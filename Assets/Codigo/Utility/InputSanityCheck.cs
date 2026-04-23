using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace ExoBeasts.Diagnostics
{
    /// <summary>
    /// Script de diagnóstico para verificar se os eventos do Input System estão chegando
    /// e se a autoridade de rede está correta.
    /// </summary>
    public class InputSanityCheck : NetworkBehaviour
    {
        [Header("Status de Diagnóstico")]
        [SerializeField] private bool debugLogsEnabled = true;
        [SerializeField] private Vector2 lastReceivedMove;

        public override void OnNetworkSpawn()
        {
            if (debugLogsEnabled)
                Debug.Log($"[InputSanity] Objeto Spawnado. IsOwner: {IsOwner}, ClientId: {OwnerClientId}, Path: {gameObject.name}");
        }

        /// <summary>
        /// Vincule este método ao Unity Event "Move" do componente PlayerInput.
        /// </summary>
        public void OnMove(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;

            lastReceivedMove = ctx.ReadValue<Vector2>();
            
            if (debugLogsEnabled)
            {
                Debug.Log($"[InputSanity] OnMove Recebido! Valor: {lastReceivedMove} | Fase: {ctx.phase}");
            }
        }

        /// <summary>
        /// Vincule este método ao Unity Event "Jump" do componente PlayerInput.
        /// </summary>
        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;

            if (ctx.started && debugLogsEnabled)
            {
                Debug.Log("[InputSanity] OnJump (Started) Recebido!");
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Verifica se o TimeScale ou Pausa global estão afetando algo
            if (Time.frameCount % 120 == 0 && debugLogsEnabled)
            {
                Debug.Log($"[InputSanity] Heartbeat - IsOwner: {IsOwner} | Time.timeScale: {Time.timeScale} | Frame: {Time.frameCount}");
            }
        }
    }
}
