using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// ── HabilidadeVooGracioso ────────────────────────────────
/// Habilidade 1 da Coruja: ao ativar no ar, a personagem flutua por alguns
/// segundos e a próxima flecha ganha dano bônus + área de explosão.
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Voo Gracioso", menuName = "ExoBeasts/Personagens/Coruja/Habilidade/Voo Gracioso")]
public class HabilidadeVooGracioso : Ability
{
    [Header("Configuracoes da Habilidade")]
    public float jumpHeightModifier = 1.5f;
    public float staticAimDuration = 3f;
    public float bonusDamageMultiplier = 1.5f;
    public float bonusExplosionRadius = 5f;

    public override bool Activate(GameObject quemUsou)
    {
        Debug.Log("[VooGracioso] Activate() chamado!");

        PlayerMovement movement = quemUsou.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogWarning("[VooGracioso] PlayerMovement não encontrado!");
            return false;
        }

        if (movement.isGrounded)
        {
            Debug.Log("[VooGracioso] Jogador está no chão — habilidade requer estar no ar!");
            return false;
        }

        PlayerShooting shooting = quemUsou.GetComponent<PlayerShooting>();

        // Aplica flutuação
        movement.isFloating = true;
        movement.floatDuration = staticAimDuration;
        movement.jumpHeightModifier = jumpHeightModifier;
        Debug.Log($"[VooGracioso] Flutuando por {staticAimDuration}s. jumpMod={jumpHeightModifier}");

        // Aplica bônus na próxima flecha
        if (shooting != null)
        {
            shooting.SetNextShotBonus(bonusDamageMultiplier, bonusExplosionRadius);
            Debug.Log($"[VooGracioso] Próxima flecha: {bonusDamageMultiplier}x dano, raio {bonusExplosionRadius}");
        }
        else
        {
            Debug.LogWarning("[VooGracioso] PlayerShooting não encontrado!");
        }

        // Reseta modificadores após a duração
        movement.StartCoroutine(ResetAfterFloat(movement));

        return true;
    }

    private IEnumerator ResetAfterFloat(PlayerMovement movement)
    {
        yield return new WaitForSeconds(staticAimDuration + 0.5f);
        if (movement != null)
        {
            movement.jumpHeightModifier = 1f;
            movement.isFloating = false;
            Debug.Log("[VooGracioso] Float encerrado. Modificadores resetados.");
        }
    }
}
