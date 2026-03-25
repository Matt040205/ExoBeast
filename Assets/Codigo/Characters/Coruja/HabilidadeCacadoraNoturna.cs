using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── HabilidadeCacadoraNoturna ────────────────────────────
/// ScriptableObject that spawns the CacadoraNoturnaLogic beam on the server.
///
///  ▸ Only runs on IsServer — HabilidadeVooGracioso gate prevents client calls
///  ▸ Spawn position derived from firePoint for accurate beam origin
///  ▸ StartUltimateEffect called after Spawn so NetworkVariables are ready
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Cacadora Noturna", menuName = "ExoBeasts/Personagens/Coruja/Habilidade/Cacadora Noturna")]
public class HabilidadeCacadoraNoturna : Ability
{
    [Header("Configuracoes da Habilidade")]
    public float damage = 500f;
    public float range = 100f;
    public float width = 3f;

    [Tooltip("Arraste o prefab da logica da habilidade aqui.")]
    public CacadoraNoturnaLogic logicPrefab;

    public override bool Activate(GameObject quemUsou)
    {
        if (logicPrefab == null)
        {
            Debug.LogError("O prefab da logica da habilidade esta NULO no ScriptableObject 'Cacadora Noturna'!");
            return true;
        }

        if (!NetworkManager.Singleton.IsServer) return true;

        PlayerShooting shootingScript = quemUsou.GetComponent<PlayerShooting>();
        PlayerMovement movementScript = quemUsou.GetComponent<PlayerMovement>();

        Transform modelPivot = (movementScript != null) ? movementScript.GetModelPivot() : quemUsou.transform;
        Transform firePoint = (shootingScript != null && shootingScript.firePoint != null) ? shootingScript.firePoint : quemUsou.transform;

        Vector3 spawnPosition = firePoint.position;
        Quaternion spawnRotation = Quaternion.LookRotation(modelPivot.forward);

        CacadoraNoturnaLogic logic = Object.Instantiate(logicPrefab, spawnPosition, spawnRotation);
        logic.GetComponent<NetworkObject>().Spawn();
        logic.StartUltimateEffect(quemUsou, damage, range, width);

        return true;
    }
}
