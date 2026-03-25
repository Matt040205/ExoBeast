using UnityEngine;
using System.Collections;

public class PaintAbilitySystem : TowerAbilitySystem
{
    [Header("--- Níveis dos Caminhos (Interno: 0 a 4) ---")]
    // -1 = Não habilitado (Nível 0 visual)
    // 0 = Nível 1 visual
    // ...
    // 4 = Nível 5 visual (Máximo)
    public int dpsLevel = -1;
    public int controlLevel = -1;
    public int supportLevel = -1;

    [Header("--- Configurações Tinta (DPS) ---")]
    public float knockbackForce = 5f;
    public int shotsForBurst = 10;
    public GameObject explosionVFX;
    public float explosionRadius = 4f;
    public float explosionDamagePct = 0.5f;

    [Header("--- Configurações Tinta (Controle) ---")]
    [Range(0f, 1f)] public float slipChance = 0.15f;

    [Header("--- Configurações Tinta (Suporte) ---")]
    public float baseAuraRange = 5f;
    public float healTickRate = 1f;
    public LayerMask allyLayer;
    public GameObject healVFX;

    // Variáveis locais
    private int burstShotCounter = 0;
    private bool isBursting = false;

    private void Start()
    {
        StartCoroutine(SupportAuraRoutine());
    }

    // --- CHAME ISSO QUANDO O JOGADOR UPAR UM CAMINHO ---
    // Exemplo: UpgradePath(TowerPath.DPS);
    public void UpgradePath(TowerPath path)
    {
        switch (path)
        {
            case TowerPath.DPS:
                dpsLevel++;
                break;
            case TowerPath.Control:
                controlLevel++;
                break;
            case TowerPath.Support:
                supportLevel++;
                break;
        }
        // Atualiza o nível geral e path ativo apenas para referência visual, se necessário
        currentLevel++;
        activePath = path;
    }

    // Usado para definir níveis manualmente (ex: Load Game)
    public void SetLevels(int dps, int control, int support)
    {
        dpsLevel = dps;
        controlLevel = control;
        supportLevel = support;
    }
    // ----------------------------------------------------

    protected override void OnHit(EnemyHealthSystem enemyHealth)
    {
        if (enemyHealth == null) return;
        EnemyController enemy = enemyHealth.GetComponent<EnemyController>();
        if (enemy == null) return;

        // --- LÓGICA DPS (Roda se tiver nível de DPS) ---
        if (dpsLevel >= 0) // Tem pelo menos Nível 1
        {
            // Visual Nível 3 (Interno 2) -> Knockback
            if (dpsLevel >= 2)
            {
                Vector3 dir = (enemy.transform.position - transform.position).normalized;
                enemy.ApplyKnockback(dir + Vector3.up * 0.2f, knockbackForce);
            }
            // Visual Nível 4 (Interno 3) -> Burst
            if (dpsLevel >= 3 && !isBursting)
            {
                burstShotCounter++;
                if (burstShotCounter >= shotsForBurst)
                {
                    StartCoroutine(PerformBurstRoutine());
                    burstShotCounter = 0;
                }
            }
        }

        // --- LÓGICA CONTROLE (Roda em paralelo) ---
        if (controlLevel >= 0)
        {
            // Visual Nível 2 (Interno 1) -> Slow
            if (controlLevel >= 1)
            {
                // Visual Nível 3 (Interno 2) -> Slow Forte
                float slowAmount = (controlLevel >= 2) ? 0.4f : 0.2f;
                enemy.ApplySlow(slowAmount, 2f);
            }
            // Visual Nível 4 (Interno 3) -> Escorregar
            if (controlLevel >= 3 && Random.value <= slipChance)
            {
                enemy.ApplySlip();
            }
            // Visual Nível 5 (Interno 4) -> Paint Stack
            if (controlLevel >= 4)
            {
                enemy.AddPaintStack();
            }
        }

        // --- LÓGICA SUPORTE (Roda em paralelo) ---
        if (supportLevel >= 0)
        {
            // Visual Nível 3 (Interno 2) -> Cura na linha de tiro
            if (supportLevel >= 2) HealAlliesInLineOfFire(enemy.transform.position);
        }
    }

    protected override void OnKill(EnemyHealthSystem enemyHealth)
    {
        // Visual Nível 5 (Interno 4) de DPS -> Explosão
        if (dpsLevel >= 4)
        {
            Explode(enemyHealth.transform.position);
        }
    }

    IEnumerator PerformBurstRoutine()
    {
        isBursting = true;
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(0.1f);
            tower.Shoot();
        }
        isBursting = false;
    }

    void Explode(Vector3 position)
    {
        if (explosionVFX) Instantiate(explosionVFX, position, Quaternion.identity);
        Collider[] hits = Physics.OverlapSphere(position, explosionRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyHealthSystem hp = hit.GetComponent<EnemyHealthSystem>();
                if (hp != null) hp.TakeDamage(20f * explosionDamagePct);
            }
        }
    }

    void HealAlliesInLineOfFire(Vector3 enemyPos)
    {
        if (tower.firePoint == null) return;

        float dist = Vector3.Distance(tower.firePoint.position, enemyPos);
        Vector3 dir = (enemyPos - tower.firePoint.position).normalized;

        RaycastHit[] hits = Physics.RaycastAll(tower.firePoint.position, dir, dist, allyLayer);
        foreach (var hit in hits) ApplyHeal(hit.collider.gameObject);
    }

    IEnumerator SupportAuraRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(healTickRate);

            // Visual Nível 2 (Interno 1) -> Aura Ativa
            if (supportLevel >= 1)
            {
                // Visual Nível 4 (Interno 3) -> Range Maior
                float currentRange = baseAuraRange * (supportLevel >= 3 ? 1.5f : 1f);

                Collider[] allies = Physics.OverlapSphere(transform.position, currentRange, allyLayer);
                foreach (var ally in allies) ApplyHeal(ally.gameObject);
            }
        }
    }

    void ApplyHeal(GameObject target)
    {
        float healAmount = 5f;

        // Visual Nível 4 (Interno 3) de Suporte -> Cura +50%
        if (supportLevel >= 3) healAmount *= 1.5f;

        PlayerHealthSystem player = target.GetComponent<PlayerHealthSystem>();

        if (player != null)
        {
            player.Heal(healAmount);

            // Visual Nível 5 (Interno 4) de Suporte -> BUFF
            // Agora checa especificamente o supportLevel, ignorando os outros
            if (supportLevel >= 4)
            {
                player.ApplyBuffs(1.5f, 1.3f, 2.0f);
            }
        }

        TowerController otherTower = target.GetComponent<TowerController>();
        if (otherTower != null && otherTower != tower) otherTower.Heal(healAmount);
    }
}