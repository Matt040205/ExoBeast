using UnityEngine;
using FMODUnity;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ── HabilidadePerseguindoPresas ──────────────────────────
/// Habilidade 2 da Coruja: marca todos os inimigos ativos, fazendo-os
/// receber mais dano e ficarem visíveis (highlight) por uma duração.
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Perseguindo as Presas", menuName = "ExoBeasts/Personagens/Coruja/Habilidade/Perseguindo as Presas")]
public class HabilidadePerseguindoPresas : Ability
{
    [Header("Configuracoes da Habilidade")]
    public float markDuration = 5f;
    public float bonusDamageMultiplier = 1.25f;

    [Header("FMOD")]
    [EventRef]
    public string eventoTEC = "event:/SFX/TEC";

    public override bool Activate(GameObject quemUsou)
    {
        Debug.Log("[PerseguindoPresas] Activate() chamado!");

        // Encontra todos os inimigos ativos na cena
        EnemyHealthSystem[] enemies = Object.FindObjectsByType<EnemyHealthSystem>(FindObjectsSortMode.None);

        if (enemies.Length == 0)
        {
            Debug.Log("[PerseguindoPresas] Nenhum inimigo encontrado na cena.");
            return true; // Consome habilidade mesmo sem inimigos
        }

        Debug.Log($"[PerseguindoPresas] Marcando {enemies.Length} inimigos por {markDuration}s ({bonusDamageMultiplier}x dano)");

        List<EnemyHealthSystem> markedEnemies = new List<EnemyHealthSystem>();

        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                enemy.ApplyMarkedStatus(bonusDamageMultiplier);
                markedEnemies.Add(enemy);
                Debug.Log($"[PerseguindoPresas] Marcado: {enemy.gameObject.name}");
            }
        }

        // Usa MonoBehaviour do jogador para rodar a coroutine de remoção
        MonoBehaviour mb = quemUsou.GetComponent<MonoBehaviour>();
        if (mb != null)
        {
            mb.StartCoroutine(RemoveMarksAfterDuration(markedEnemies));
        }

        return true;
    }

    private IEnumerator RemoveMarksAfterDuration(List<EnemyHealthSystem> enemies)
    {
        yield return new WaitForSeconds(markDuration);

        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.ApplyMarkedStatus(1.0f);
            }
        }

        Debug.Log("[PerseguindoPresas] Marcação removida de todos os inimigos.");
    }
}
