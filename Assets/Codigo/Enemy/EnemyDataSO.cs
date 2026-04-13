using UnityEngine;

public enum EnemyType { Terrestre, Voador }

[CreateAssetMenu(fileName = "New Enemy Data", menuName = "ScriptableObjects/Base de Dados/Enemy")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Prefab")]
    public GameObject enemyPrefab;

    [Header("Tipo de Inimigo")]
    public EnemyType enemyType = EnemyType.Terrestre;

    [Header("Status B�sicos")]
    public float baseHP = 100f;
    public float baseATQ = 10f;
    public float moveSpeed = 3f;
    public float attackSpeed = 1f;
    [Range(0f, 1f)]
    public float baseArmor = 0f;

    [Header("Escala por N�vel")]
    public float hpPerLevel = 10f;
    public float atqPerLevel = 2f;
    public float speedPerLevel = 0.5f;
    [Range(0f, 1f)]
    public float armorPerLevel = 0.01f;

    [Header("Recompensas")]
    public int geoditasOnDeath = 1;
    [Range(0f, 1f)]
    public float etherDropChance = 0.1f;

    public float GetHealth(int level) => baseHP + (level * hpPerLevel);
    public float GetDamage(int level) => baseATQ + (level * atqPerLevel);
    public float GetMoveSpeed(int level) => moveSpeed + (level * speedPerLevel);
    public float GetArmor(int level) => Mathf.Clamp01(baseArmor + (level * armorPerLevel));
}