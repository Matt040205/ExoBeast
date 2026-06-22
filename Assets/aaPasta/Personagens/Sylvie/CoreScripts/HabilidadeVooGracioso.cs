using UnityEngine;
using Unity.Netcode;

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

        if (!movement.IsAirborneForAbility())
        {
            Debug.Log("[VooGracioso] Jogador está no chão — habilidade requer estar no ar!");
            return false;
        }

        // Delegar ao VooGraciosoLogic (NetworkBehaviour) para sincronização correta
        VooGraciosoLogic logic = quemUsou.GetComponent<VooGraciosoLogic>();
        if (logic == null)
        {
            Debug.LogWarning("[VooGracioso] VooGraciosoLogic não encontrado no player!");
            return false;
        }

        logic.StartEffect(quemUsou, jumpHeightModifier, staticAimDuration, bonusDamageMultiplier, bonusExplosionRadius, null, this);
        Debug.Log($"[VooGracioso] StartEffect delegado ao VooGraciosoLogic. jumpMod={jumpHeightModifier}, dur={staticAimDuration}");
        return true;
    }
}
