using System;

/// <summary>
/// ── EnemyEvents ──────────────────────────────────────────
/// Padrão Observer isolado para tratar os eventos de IA
/// sem acoplar regras de rede ou as dependências da UI.
/// Sempre disparado do Servidor/Host.
/// ───────────────────────────────────────────────────────
/// </summary>
public static class EnemyEvents
{
    // O parâmetro 'int' serve para identificar qual caminho/rota
    public static Action<int> OnEnemySpawned;
    public static Action<int> OnEnemyHalfway;
    public static Action OnEnemyReachedBase;
}
