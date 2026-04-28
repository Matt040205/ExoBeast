using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Sync;

[RequireComponent(typeof(BoxCollider))]
public class Espinhos : TrapLogicBase
{
    public float dano = 10f;
    public float tempoAtivo = 1.5f;
    public float tempoRecarga = 3f;

    private readonly List<EnemyHealthSystem> inimigosNaArea = new List<EnemyHealthSystem>();

    private bool isServerMode;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ConfigureTrap();

        if (IsServer)
        {
            isServerMode = true;
            StartCoroutine(CicloEspinhos());
        }
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            isServerMode = true;
            ConfigureTrap();
            StartCoroutine(CicloEspinhos());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServerMode || !other.CompareTag("Enemy"))
            return;

        EnemyHealthSystem enemyHealth = other.GetComponent<EnemyHealthSystem>();
        if (enemyHealth != null && !inimigosNaArea.Contains(enemyHealth))
            inimigosNaArea.Add(enemyHealth);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isServerMode || !other.CompareTag("Enemy"))
            return;

        EnemyHealthSystem enemyHealth = other.GetComponent<EnemyHealthSystem>();
        if (enemyHealth != null)
            inimigosNaArea.Remove(enemyHealth);
    }

    private void ConfigureTrap()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private IEnumerator CicloEspinhos()
    {
        while (true)
        {
            yield return new WaitForSeconds(tempoRecarga);

            SetTrapVisualState(true);
            AplicarDano();

            yield return new WaitForSeconds(tempoAtivo);
            SetTrapVisualState(false);
        }
    }

    private void AplicarDano()
    {
        DamageContext damageContext = new DamageContext(BuilderClientId, false, DamageFeedbackMode.AllObservers);

        for (int i = inimigosNaArea.Count - 1; i >= 0; i--)
        {
            EnemyHealthSystem enemyHealth = inimigosNaArea[i];
            if (enemyHealth == null)
            {
                inimigosNaArea.RemoveAt(i);
                continue;
            }

            enemyHealth.TakeDamage(dano, damageContext);
        }
    }

    private void SetTrapVisualState(bool isActive)
    {
        if (TryResolveVisual(out NetworkedTrapVisual trapVisual))
            trapVisual.SetActivationStateServer(isActive);
    }
}
