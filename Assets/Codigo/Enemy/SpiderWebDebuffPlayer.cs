using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class SpiderWebDebuffPlayer : MonoBehaviour
{
    private PlayerHealthSystem healthSystem;
    private PlayerMovement movement;

    private int hitCount = 0;
    private float slowTimer = 0f;
    private const float SLOW_DURATION = 3f;
    
    private int spacePressesRemaining = 4;
    private bool isTrapped = false;

    private Coroutine debuffCoroutine;

    private void Awake()
    {
        healthSystem = GetComponent<PlayerHealthSystem>();
        movement = GetComponent<PlayerMovement>();
    }

    public void OnHit(bool enableTrap)
    {
        if (healthSystem == null || movement == null) return;

        // Se já está preso, ignora novos acúmulos
        if (isTrapped) return;

        hitCount++;
        slowTimer = SLOW_DURATION;

        // Aplica lentidão (50% de velocidade) no servidor
        if (NetworkManager.Singleton.IsServer)
        {
            healthSystem.speedMultiplier.Value = 0.5f;
        }

        // Verifica se ultrapassou o limite de teias para prender
        if (hitCount >= 5 && enableTrap)
        {
            TrapPlayer();
        }
        else
        {
            // Renova ou inicia a corrotina de debuff
            if (debuffCoroutine != null) StopCoroutine(debuffCoroutine);
            debuffCoroutine = StartCoroutine(SlowCountdown());
        }
    }

    private void TrapPlayer()
    {
        isTrapped = true;
        spacePressesRemaining = 4;

        if (NetworkManager.Singleton.IsServer)
        {
            movement.netIsWebTrapped.Value = true;
            // Opcional: pode manter a velocidade reduzida para quando ele se libertar
            healthSystem.speedMultiplier.Value = 0.5f;
        }

        if (debuffCoroutine != null) StopCoroutine(debuffCoroutine);
    }

    public void RegisterSpacePress()
    {
        if (!isTrapped) return;

        spacePressesRemaining--;
        Debug.Log($"[SpiderWebDebuffPlayer] Cliques restantes para se libertar: {spacePressesRemaining}");

        if (spacePressesRemaining <= 0)
        {
            ReleasePlayer();
        }
    }

    private void ReleasePlayer()
    {
        isTrapped = false;
        hitCount = 0;

        if (NetworkManager.Singleton.IsServer)
        {
            movement.netIsWebTrapped.Value = false;
            healthSystem.speedMultiplier.Value = 1f; // restaura velocidade total
        }

        Destroy(this);
    }

    private IEnumerator SlowCountdown()
    {
        while (slowTimer > 0)
        {
            slowTimer -= Time.deltaTime;
            yield return null;
        }

        // Se o tempo da lentidão expirou sem ele ficar preso
        if (!isTrapped)
        {
            hitCount = 0;
            if (NetworkManager.Singleton.IsServer)
            {
                healthSystem.speedMultiplier.Value = 1f;
            }
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (movement != null) movement.netIsWebTrapped.Value = false;
            if (healthSystem != null) healthSystem.speedMultiplier.Value = 1f;
        }
    }
}
