using UnityEngine;

[CreateAssetMenu(fileName = "Bomba de Spray", menuName = "ExoBeasts/Personagens/Polvo/Habilidade/Bomba de Spray")]
public class HabilidadeBombaSpray : Ability
{
    [Header("Configurações da Bomba")]
    public float throwForce = 15f;
    public float explosionRadius = 6f;
    public float cloudDuration = 4f;

    [Tooltip("Arraste o prefab da LATA aqui")]
    public BombaSprayProjectile projectilePrefab;

    [Tooltip("Layers que o raio da mira pode acertar (geralmente Default, Ground, Enemy)")]
    public LayerMask aimLayers = ~0;

    public override bool Activate(GameObject quemUsou)
    {
        if (projectilePrefab == null)
        {
            return false;
        }

        Vector3 spawnPos = quemUsou.transform.position + Vector3.up * 1.5f;
        PlayerShooting shootingScript = quemUsou.GetComponent<PlayerShooting>();

        if (shootingScript != null && shootingScript.firePoint != null)
        {
            spawnPos = shootingScript.firePoint.position;
        }

        Vector3 throwDirection = GetAimDirection(quemUsou, spawnPos);

        BombaSprayProjectile bomba = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(throwDirection));

        bomba.Launch(throwDirection * throwForce, explosionRadius, cloudDuration);

        CommanderAbilityController abilityScript = quemUsou.GetComponent<CommanderAbilityController>();
        if (abilityScript != null)
        {
            abilityScript.SetAbilityUsage(this, true);
        }

        return true;
    }

    private Vector3 GetAimDirection(GameObject quemUsou, Vector3 originPoint)
    {
        Camera cam = Camera.main;
        if (cam == null) return quemUsou.transform.forward;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        Vector3 targetPoint;

        if (Physics.Raycast(ray, out hit, 100f, aimLayers))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(100f);
        }

        return (targetPoint - originPoint).normalized;
    }
}