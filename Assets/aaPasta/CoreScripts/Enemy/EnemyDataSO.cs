using UnityEngine;

public enum EnemyType { Terrestre, Voador }

[CreateAssetMenu(fileName = "New Enemy Data", menuName = "ScriptableObjects/Base de Dados/Enemy")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Prefab")]
    public GameObject enemyPrefab;

    [Header("Tipo de Inimigo")]
    public EnemyType enemyType = EnemyType.Terrestre;

    [Header("Status Bsicos")]
    public float baseHP = 100f;
    public float baseATQ = 10f;
    [Tooltip("Dano fixo causado ao objetivo principal (Base) ao completar a rota.")]
    public float damageToBase = 10f;
    public float moveSpeed = 3f;
    public float attackSpeed = 1f;
    [Range(0f, 1f)]
    public float baseArmor = 0f;

    [Header("Escala por Nvel")]
    public float hpPerLevel = 10f;
    public float atqPerLevel = 2f;
    public float speedPerLevel = 0.5f;
    [Range(0f, 1f)]
    public float armorPerLevel = 0.01f;

    [Header("Recompensas")]
    public int geoditasOnDeath = 1;
    [Range(0f, 1f)]
    public float etherDropChance = 0.1f;

    public float GetHealth(int level) => Mathf.Round(baseHP * (1f + ((level - 1) * 0.15f)));
    public float GetDamage(int level) => baseATQ + ((level - 1) * atqPerLevel);
    public float GetMoveSpeed(int level) => moveSpeed + ((level - 1) * speedPerLevel);
    public float GetArmor(int level) => Mathf.Clamp01(baseArmor + ((level - 1) * armorPerLevel));
}