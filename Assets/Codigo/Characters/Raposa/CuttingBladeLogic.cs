using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using Unity.Netcode;

/// <summary>
/// ── CuttingBladeLogic ────────────────────────────────────
/// Executes the fox's dash-with-area-damage ability.
///
///  ▸ Owner drives movement: dash runs locally for responsiveness
///  ▸ Server validates: PerformDashDamageServerRpc applies damage authoritatively
///  ▸ Remote owner: server delegates dash execution via owner-targeted ClientRpc
///  ▸ Kill-reset: server notifies owner via ClientRpc to reset cooldown on kill
/// ─────────────────────────────────────────────────────────
/// </summary>
public class CuttingBladeLogic : NetworkBehaviour
{
    private CharacterController controller;
    private Transform modelPivot;
    private float dashDistance;
    private float damage;
    private string eventoDash;
    private CommanderAbilityController abilityController;
    private Ability sourceAbility;
    private bool resetCooldownOnKill;

    [Header("Juice Configs")]
    [SerializeField] private CameraShakeConfig dashShake = new CameraShakeConfig(1.5f, 10f, 0.15f);

    [Header("Rastro do Dash (Mesh Baking)")]
    [SerializeField] private float trailSpacing = 1.5f;

    [Header("Hit VFX (Star Guardians)")]
    [SerializeField] private GameObject personalHitVfxPrefab;

    private PlayerMovement playerMovement;

    public void StartDash(GameObject quemUsou, CharacterController cont, Transform pivot, float dist, float dmg, string som, CommanderAbilityController abCont, Ability ability, bool resetOnKill)
    {
        dashDistance = dist;
        damage = dmg;
        eventoDash = som;
        abilityController = abCont;
        sourceAbility = ability;
        resetCooldownOnKill = resetOnKill;
        modelPivot = pivot;

        if (IsOwner)
        {
            // Caminho direto para o jogador local
            controller = cont;
            playerMovement = quemUsou.GetComponent<PlayerMovement>();
            abilityController.SetAbilityUsage(sourceAbility, true);
            StartCoroutine(DashCoroutine(quemUsou));
        }
        else if (IsServer)
        {
            // Servidor executando para jogador remoto — delega ao owner via ClientRpc
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
            };
            ExecuteDashOnOwnerClientRpc(dist, dmg, som, resetOnKill, clientRpcParams);
        }
    }

    // Executa o dash na máquina do owner (tem acesso ao CharacterController local)
    [ClientRpc]
    private void ExecuteDashOnOwnerClientRpc(float dist, float dmg, string som, bool resetOnKill, ClientRpcParams _ = default)
    {
        dashDistance = dist;
        damage = dmg;
        eventoDash = som;
        resetCooldownOnKill = resetOnKill;
        modelPivot = transform;
        controller = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();
        abilityController = GetComponent<CommanderAbilityController>();
        // sourceAbility fica null aqui — SetAbilityUsage/ResetCooldown tolerarão null via null-check
        StartCoroutine(DashCoroutine(gameObject));
    }

    private IEnumerator DashCoroutine(GameObject quemUsou)
    {
        if (playerMovement != null)
        {
            playerMovement.isDashing = true;
        }

        MeshTrail trail = quemUsou.GetComponent<MeshTrail>();
        if (trail == null) trail = quemUsou.GetComponentInChildren<MeshTrail>();
        // O trigger antigo (via tempo) foi removido do MeshTrail, 
        // agora o trail será criado via interpolação de pontos mais abaixo.

        if (!string.IsNullOrEmpty(eventoDash))
        {
            RuntimeManager.PlayOneShot(eventoDash, transform.position);
        }

        Vector3 startPosition = transform.position;
        Vector3 dashDirection = modelPivot.forward;

        if (IsOwner)
        {
            JuiceEvents.OnCameraShake?.Invoke(dashDirection, dashShake.amplitude, dashShake.frequency, dashShake.duration);
        }

        Vector3 targetPosition = startPosition + (dashDirection * dashDistance);
        int obstacleMask = LayerMask.GetMask("Default", "Ground", "Terrain");

        RaycastHit wallHit;
        if (Physics.Raycast(startPosition + Vector3.up, dashDirection, out wallHit, dashDistance, obstacleMask))
        {
            targetPosition = wallHit.point - (dashDirection * 0.5f);
        }

        controller.enabled = false;

        Vector3 finalPosition = targetPosition;
        int groundMask = LayerMask.GetMask("Default", "Ground");

        RaycastHit groundHit;
        if (Physics.Raycast(targetPosition + Vector3.up * 0.5f, Vector3.down, out groundHit, 5f, groundMask))
        {
            finalPosition = groundHit.point + (Vector3.up * (controller.height / 2f));
        }

        // Owner já cria os rastros localmente instantaneamente
        SpawnTrailAlongPath(startPosition, finalPosition);

        // HIT VFX PERSONALIZADO (ZERO LATENCY LOCAL PARA O DONO)
        if (IsOwner && personalHitVfxPrefab != null)
        {
            float actualDist = Vector3.Distance(startPosition, finalPosition);
            Vector3 dashDir = (finalPosition - startPosition).normalized;
            if (actualDist < 0.1f) dashDir = transform.forward;
            
            RaycastHit[] localHits = Physics.SphereCastAll(startPosition, 2f, dashDir, actualDist);
            List<EnemyHealthSystem> enemiesHitLocal = new List<EnemyHealthSystem>();
            
            foreach (var hit in localHits)
            {
                EnemyHealthSystem vidaInimigo = hit.collider.GetComponent<EnemyHealthSystem>();
                if (vidaInimigo != null && !enemiesHitLocal.Contains(vidaInimigo))
                {
                    enemiesHitLocal.Add(vidaInimigo);
                    Vector3 contactPoint = hit.point != Vector3.zero ? hit.point : hit.collider.ClosestPoint(transform.position);
                    Vector3 normal = hit.normal != Vector3.zero ? hit.normal : (transform.position - contactPoint).normalized;
                    Quaternion hitRot = normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
                    
                    GlobalVFXPool.GetVFX(personalHitVfxPrefab, contactPoint, hitRot, 2f);
                    RequestMeleeHitVfxServerRpc(contactPoint, hitRot);
                }
            }
        }

        transform.position = finalPosition;

        yield return null;
        controller.enabled = true;

        PerformDashDamageServerRpc(startPosition, finalPosition, damage, resetCooldownOnKill);

        if (playerMovement != null)
        {
            playerMovement.isDashing = false;
        }

        // Disable instead of destroy — this component lives on the player's NetworkObject
        this.enabled = false;

        if (abilityController != null)
        {
            abilityController.SetAbilityUsage(sourceAbility, false);
        }
    }

    [ServerRpc]
    private void PerformDashDamageServerRpc(Vector3 start, Vector3 end, float dashDamage, bool resetOnKill)
    {
        float actualDistance = Vector3.Distance(start, end);
        Vector3 dashDirection = (end - start).normalized;
        if (actualDistance < 0.1f) dashDirection = transform.forward;

        float dashRadius = 2f;
        RaycastHit[] hits = Physics.SphereCastAll(start, dashRadius, dashDirection, actualDistance);

        List<EnemyHealthSystem> enemiesHit = new List<EnemyHealthSystem>();
        bool matouAlguem = false;

        foreach (var hit in hits)
        {
            EnemyHealthSystem vidaInimigo = hit.collider.GetComponent<EnemyHealthSystem>();
            if (vidaInimigo != null && !enemiesHit.Contains(vidaInimigo))
            {
                enemiesHit.Add(vidaInimigo);
                bool inimigoMorreu = vidaInimigo.TakeDamage(dashDamage);
                if (inimigoMorreu)
                {
                    matouAlguem = true;
                }
            }
        }

        if (resetOnKill && matouAlguem)
        {
            NotifyResetCooldownClientRpc();
        }

        BroadcastTrailExplosionClientRpc(start, end);
    }

    [ClientRpc]
    private void NotifyResetCooldownClientRpc()
    {
        if (IsOwner && abilityController != null)
        {
            abilityController.ResetCooldown(sourceAbility);
        }
    }

    [ClientRpc]
    private void BroadcastTrailExplosionClientRpc(Vector3 start, Vector3 end)
    {
        if (IsOwner) return; // O dono já processou localmente
        SpawnTrailAlongPath(start, end);
    }

    private void SpawnTrailAlongPath(Vector3 startPos, Vector3 endPos)
    {
        MeshTrail trailManager = GetComponent<MeshTrail>();
        if (trailManager == null) trailManager = GetComponentInChildren<MeshTrail>();
        if (trailManager == null) return;

        float distance = Vector3.Distance(startPos, endPos);
        if (distance < 0.1f) return;

        int cloneCount = Mathf.Max(1, Mathf.FloorToInt(distance / trailSpacing));
        Vector3 direction = (endPos - startPos).normalized;
        Quaternion rotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : transform.rotation;

        for (int i = 1; i <= cloneCount; i++)
        {
            float t = (float)i / cloneCount;
            Vector3 spawnPos = Vector3.Lerp(startPos, endPos, t);
            
            // Faz o Bake Mesh em cada ponto espalhado, usando o modelPivot do owner como referência de malha
            trailManager.SpawnGhostAt(spawnPos, rotation, modelPivot);
        }
    }

    [ServerRpc]
    private void RequestMeleeHitVfxServerRpc(Vector3 position, Quaternion rotation)
    {
        PlayMeleeHitVfxClientRpc(position, rotation);
    }

    [ClientRpc]
    private void PlayMeleeHitVfxClientRpc(Vector3 position, Quaternion rotation)
    {
        if (IsOwner) return; // O dono já tocou instantaneamente
        if (personalHitVfxPrefab != null)
        {
            GlobalVFXPool.GetVFX(personalHitVfxPrefab, position, rotation, 2f);
        }
    }
}
