using UnityEngine;
using System.Collections;

public class ObraPrimaLogic : MonoBehaviour
{
    private float _damagePerShot; // Dano por "tiro"
    private int _shotsCount;      // Quantidade de tiros
    private float _duration;      // Duração total
    private float _radius;        // Raio de alcance
    private float _silenceDur;
    private Transform _owner;

    // Atualizei os parâmetros para receber tiros e dano por tiro
    public void StartUltimate(GameObject owner, float duration, int shotsCount, float damagePerShot, float radius, float silenceDur)
    {
        _owner = owner.transform;
        _duration = duration;
        _shotsCount = shotsCount; // Quantidade de pulsos de dano
        _damagePerShot = damagePerShot;
        _radius = radius;
        _silenceDur = silenceDur;

        // Inicia a rotina de tiros
        StartCoroutine(DealDamageRoutine());
    }

    private IEnumerator DealDamageRoutine()
    {
        // Calcula o tempo de intervalo entre cada tiro baseada na duração total
        // Ex: Se durar 5s e tiver 5 tiros, o intervalo é 1s.
        float interval = _duration / _shotsCount;

        for (int i = 0; i < _shotsCount; i++)
        {
            ApplyDamagePulse();

            // Espera o tempo do intervalo antes do próximo tiro
            yield return new WaitForSeconds(interval);
        }

        // Destroi o objeto após o último tiro terminar
        Destroy(gameObject);
    }

    private void ApplyDamagePulse()
    {
        // Detecta inimigos na área
        Collider[] hits = Physics.OverlapSphere(transform.position, _radius);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                // Tenta pegar o script de vida do inimigo
                // Substitua 'EnemyHealth' pelo nome exato do seu script de vida
                var enemyHealth = hit.GetComponent<EnemyHealthSystem>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(_damagePerShot);
                }

                // Lógica dos status (Pintado/Silence)
                // var enemyStatus = hit.GetComponent<EnemyStatus>();
                // if (enemyStatus != null) 
                // { 
                //     enemyStatus.ApplySilence(_silenceDur); 
                //     enemyStatus.ApplySlow(0.8f); 
                // }
            }
        }
    }

    void Update()
    {
        // Segue o jogador
        if (_owner != null) transform.position = _owner.position;

        // Gira o visual (apenas visual)
        transform.Rotate(Vector3.up * 720 * Time.deltaTime);
    }

    // Opcional: Para visualizar o raio no editor da Unity
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _radius > 0 ? _radius : 1f);
    }
}