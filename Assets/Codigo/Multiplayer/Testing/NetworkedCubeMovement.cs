using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Testing
{
    /// <summary>
    /// ── NetworkedCubeMovement ────────────────────────────
    /// Movimento WASD para o cubo de teste na cena Network Test.unity.
    ///
    ///  ▸ Apenas o dono (IsOwner) processa input
    ///  ▸ Posicao replicada via ClientNetworkTransform no prefab
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class NetworkedCubeMovement : NetworkBehaviour
    {
        [SerializeField] private float speed = 5f;

        private void Update()
        {
            if (!IsOwner) return;

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            Vector3 move = new Vector3(h, 0f, v) * speed * Time.deltaTime;
            transform.Translate(move, Space.World);
        }
    }
}
