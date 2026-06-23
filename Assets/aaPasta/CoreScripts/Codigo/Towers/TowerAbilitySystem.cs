using UnityEngine;
using System.Collections;

public enum TowerPath { None, DPS, Control, Support }

[RequireComponent(typeof(TowerController))]
public abstract class TowerAbilitySystem : MonoBehaviour
{
    [Header("Estado Geral")]
    public TowerPath activePath = TowerPath.None;
    [Range(1, 5)] public int currentLevel = 1;

    protected TowerController tower;

    protected virtual void Awake()
    {
        tower = GetComponent<TowerController>();
    }

    protected virtual void OnEnable()
    {
        tower.OnTargetDamaged += OnHit;
        tower.OnEnemyKilled += OnKill;
    }

    protected virtual void OnDisable()
    {
        tower.OnTargetDamaged -= OnHit;
        tower.OnEnemyKilled -= OnKill;
    }

    // Chamado pelo TowerController quando upa
    public void UpdateAbilityState(TowerPath newPath, int newLevel)
    {
        this.activePath = newPath;
        this.currentLevel = newLevel;
        OnAbilityUpdated(); // Gancho para efeitos visuais
    }

    // Métodos que os filhos VÃO sobrescrever (Override)
    protected virtual void OnHit(EnemyHealthSystem enemy) { }
    protected virtual void OnKill(EnemyHealthSystem enemy) { }
    protected virtual void OnAbilityUpdated() { }
}