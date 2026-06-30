using UnityEngine;

/// <summary>
/// ── TrilhaVooSilencioso ──────────────────────────────────
/// Upgrade path data: triggers brief invisibility on Voo Gracioso activation.
///
///  ▸ Pure data class — invisibility duration read by VooGraciosoLogic
///  ▸ No network logic needed; visibility state synced via NetworkAnimator or ClientRpc
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Trilha Voo Silencioso", menuName = "ExoBeasts/Personagens/Coruja/Trilha/Voo Silencioso")]
public class TrilhaVooSilencioso : UpgradePath
{
    [Header("Configuracoes da Trilha")]
    public float invisibilityDuration = 1.5f;
    // Invisibility logic implemented in VooGraciosoLogic
}
