using UnityEngine;

public class DebugDamage : MonoBehaviour
{
    private PlayerHealthSystem playerHealthSystem;

    void Start()
    {
        // Tenta obter o componente PlayerHealthSystem uma única vez no início.
        playerHealthSystem = GetComponent<PlayerHealthSystem>();

        if (playerHealthSystem == null)
        {
            Debug.LogError("PlayerHealthSystem não encontrado no mesmo GameObject que DebugDamage.");
        }
    }

    void Update()
    {
        // Verifica se a referência é válida antes de tentar usar.
        if (playerHealthSystem != null)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                playerHealthSystem.TakeDamage(10);
                Debug.Log("Dano aplicado! Vida atual: " + playerHealthSystem.currentHealth);
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                playerHealthSystem.Heal(20);
                Debug.Log("Cura aplicada! Vida atual: " + playerHealthSystem.currentHealth);
            }
        }
        else
        {
            // Tenta encontrar o player se a referência foi perdida
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerHealthSystem = player.GetComponent<PlayerHealthSystem>();
                if (playerHealthSystem != null)
                {
                    Debug.Log("Referência para PlayerHealthSystem encontrada com sucesso!");
                }
            }
        }
    }
}