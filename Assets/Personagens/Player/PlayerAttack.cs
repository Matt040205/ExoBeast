using UnityEngine;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Sync;

public class PlayerAttack : MonoBehaviour
{
    [Header("Configurações de Ataque")]
    public float damage = 25f;
    public float fireRate = 0.5f;
    public float range = 100f;

    private float nextTimeToFire = 0f;

    [Header("Câmera e UI")]
    public Camera mainCamera;
    public GameObject crosshairDot;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (crosshairDot != null) crosshairDot.SetActive(true);
    }

    void Update()
    {
        // Bloqueio de rede: apenas o dono deste personagem pode atirar
        NetworkObject netObj = GetComponentInParent<NetworkObject>();
        if (netObj != null && !netObj.IsOwner) return;

        if (Input.GetMouseButton(0) && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        RaycastHit hit;
        if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit, range))
        {
            NetworkedEnemy networkedEnemy = hit.transform.GetComponent<NetworkedEnemy>();
            if (networkedEnemy != null && networkedEnemy.IsSpawned)
            {
                // O servidor captura o ID automaticamente através do ServerRpcParams
                networkedEnemy.TakeDamageServerRpc(damage, 0f, false);
            }
        }
    }
}