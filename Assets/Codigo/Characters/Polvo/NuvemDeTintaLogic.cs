using UnityEngine;
using System.Collections.Generic;

public class NuvemDeTintaLogic : MonoBehaviour
{
    public GameObject vfxFumacaPrefab;

    [Tooltip("Quanto da velocidade original vai SOBRAR. Ex: 0.6 mantém 60% da velocidade.")]
    public float slowFactor = 0.6f;

    private float _duration;
    private List<EnemyController> affectedEnemies = new List<EnemyController>();

    public void Setup(float duration)
    {
        _duration = duration;

        if (vfxFumacaPrefab != null)
        {
            Instantiate(vfxFumacaPrefab, transform.position, Quaternion.identity, transform);
        }

        Destroy(gameObject, _duration);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyController enemy = other.GetComponent<EnemyController>();

            if (enemy != null)
            {
                if (!affectedEnemies.Contains(enemy))
                {
                    affectedEnemies.Add(enemy);

                    // O EnemyController usa "Percentual de Redução".
                    // Se slowFactor é 0.6 (60% de sobra), a redução é 0.4 (40%).
                    float reducao = 1f - slowFactor;
                    enemy.AplicarDesaceleracao(reducao);

                    other.SendMessage("SetBlinded", true, SendMessageOptions.DontRequireReceiver);

                    Debug.Log("Inimigo entrou na fumaça (Slow aplicado): " + other.name);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyController enemy = other.GetComponent<EnemyController>();

            if (enemy != null)
            {
                if (affectedEnemies.Contains(enemy))
                {
                    enemy.RemoverDesaceleracao();

                    other.SendMessage("SetBlinded", false, SendMessageOptions.DontRequireReceiver);

                    affectedEnemies.Remove(enemy);
                    Debug.Log("Inimigo saiu da fumaça (Normal): " + other.name);
                }
            }
        }
    }

    private void OnDestroy()
    {
        foreach (EnemyController enemy in affectedEnemies)
        {
            if (enemy != null)
            {
                enemy.RemoverDesaceleracao();
                enemy.SendMessage("SetBlinded", false, SendMessageOptions.DontRequireReceiver);
            }
        }
        affectedEnemies.Clear();
    }
}