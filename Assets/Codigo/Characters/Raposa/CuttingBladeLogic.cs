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
        if (trail != null) trail.TriggerTrail();

        if (!string.IsNullOrEmpty(eventoDash))
        {
            RuntimeManager.PlayOneShot(eventoDash, transform.position);
        }

        Vector3 startPosition = transform.position;
        Vector3 dashDirection = modelPivot.forward;
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
    }

    [ClientRpc]
    private void NotifyResetCooldownClientRpc()
    {
        if (IsOwner && abilityController != null)
        {
            abilityController.ResetCooldown(sourceAbility);
        }
    }
}
