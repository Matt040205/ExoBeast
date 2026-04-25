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
        Vector3 aimForward = AbilityAimUtility.ResolveAimForward(quemUsou);
        if (aimForward.sqrMagnitude <= 0.0001f && quemUsou != null)
            aimForward = AbilityAimUtility.ResolveFlatForward(quemUsou.transform);

        // Mantem um arco leve para a lata nao colidir no proprio chao ao nascer.
        Vector3 throwDirection = (aimForward + Vector3.up * 0.15f).normalized;
        return throwDirection.sqrMagnitude > 0.0001f ? throwDirection : Vector3.forward;
    }
}
