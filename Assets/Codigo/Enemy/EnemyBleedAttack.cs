using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Adicione este componente a qualquer inimigo para que seus ataques causem Sangramento.
/// O sangramento aplica dano contínuo ao jogador por um tempo configurável,
/// independente da distância — o inimigo não precisa continuar perto para machucar.
/// Suporta múltiplas pilhas (stacks) de sangramento simultâneas.
/// </summary>
public class EnemyBleedAttack : NetworkBehaviour
{
    [Header("Sangramento")]
    [Tooltip("Dano por tick de sangramento (por segundo).")]
    public float bleedDamagePerTick = 5f;

    [Tooltip("Intervalo entre cada tick de sangramento (segundos).")]
    public float bleedTickInterval = 1f;

    [Tooltip("Duração total do sangramento (segundos).")]
    public float bleedDuration = 5f;

    [Tooltip("Máximo de pilhas de sangramento simultâneas no mesmo jogador. 0 = ilimitado.")]
    public int maxBleedStacks = 3;

    // Rastreia as coroutines de sangramento ativas por jogador (no servidor)
    private readonly Dictionary<PlayerHealthSystem, List<Coroutine>> activeBleedCoroutines
        = new Dictionary<PlayerHealthSystem, List<Coroutine>>();

    /// <summary>
    /// Chamado pelo EnemyCombatSystem ao aplicar dano — inicia o sangramento no alvo.
    /// </summary>
    public void ApplyBleed(Transform targetTransform)
    {
        if (!IsServer) return;

        PlayerHealthSystem health = targetTransform.GetComponent<PlayerHealthSystem>();
        if (health == null) health = targetTransform.GetComponentInParent<PlayerHealthSystem>();
        if (health == null) health = targetTransform.GetComponentInChildren<PlayerHealthSystem>();
        if (health == null) return;

        // Respeita o limite de stacks
        if (!activeBleedCoroutines.ContainsKey(health))
            activeBleedCoroutines[health] = new List<Coroutine>();

        List<Coroutine> stacks = activeBleedCoroutines[health];

        // Remove referências nulas (coroutines já encerradas naturalmente)
        stacks.RemoveAll(c => c == null);

        if (maxBleedStacks > 0 && stacks.Count >= maxBleedStacks)
        {
            // Cancela a pilha mais antiga para abrir espaço
            StopCoroutine(stacks[0]);
            stacks.RemoveAt(0);
        }

        Coroutine bleedCoroutine = StartCoroutine(BleedRoutine(health));
        stacks.Add(bleedCoroutine);
    }

    private IEnumerator BleedRoutine(PlayerHealthSystem health)
    {
        float elapsed = 0f;
        int targetGeneration = health.SpawnGeneration;

        while (elapsed < bleedDuration)
        {
            yield return new WaitForSeconds(bleedTickInterval);
            elapsed += bleedTickInterval;

            // Checa se o jogador ainda existe e se é a mesma "vida" (cancela se ele morreu/respawnou)
            if (health == null || health.gameObject == null || health.SpawnGeneration != targetGeneration)
                yield break;

            // Aplica o tick de sangramento (sem attacker, pois é DoT)
            health.TakeDamage(bleedDamagePerTick, transform);
        }

        // Remove esta coroutine da lista quando terminar
        if (activeBleedCoroutines.TryGetValue(health, out List<Coroutine> stacks))
            stacks.RemoveAll(c => c == null);
    }

    private void OnDisable()
    {
        // Para todos os sangramentos ativos quando o inimigo é desativado (pooling)
        StopAllCoroutines();
        activeBleedCoroutines.Clear();
    }
}
