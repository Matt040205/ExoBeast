using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── TowerBehavior ─────────────────────────────────────
/// Classe base abstrata para comportamentos especiais de torre (upgrades).
///
///  ▸ NetworkBehaviour: permite IsServer guards nos subclasses
///  ▸ Initialize(): chamado pelo TowerController ao adicionar o comportamento
///  ▸ Subclasses: devem usar OnNetworkDespawn para cleanup de eventos
/// ─────────────────────────────────────────────────────
/// </summary>
public abstract class TowerBehavior : NetworkBehaviour
{
    public TowerController towerController;

    public virtual void Initialize(TowerController owner)
    {
        this.towerController = owner;
    }
}
