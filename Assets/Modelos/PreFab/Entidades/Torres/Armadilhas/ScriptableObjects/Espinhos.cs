using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Espinhos: Armadilha que aplica dano em ciclos a inimigos na área.
/// Colisão detectada em todos os clientes, mas dano aplicado só no servidor.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class Espinhos : TrapLogicBase
{
    public float dano = 10f;
    public float tempoAtivo = 1.5f;
    public float tempoRecarga = 3f;
    public Animator animatorEspinhos;

    private List<EnemyHealthSystem> inimigosNaArea = new List<EnemyHealthSystem>();
    private bool isServerMode = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        GetComponent<BoxCollider>().isTrigger = true;
        if (IsServer)
        {
            isServerMode = true;
            StartCoroutine(CicloEspinhos());
        }
    }

    // Fallback offline
    void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            isServerMode = true;
            GetComponent<BoxCollider>().isTrigger = true;
            StartCoroutine(CicloEspinhos());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServerMode) return;
        if (other.CompareTag("Enemy"))
        {
            EnemyHealthSystem saudeInimigo = other.GetComponent<EnemyHealthSystem>();
            if (saudeInimigo != null && !inimigosNaArea.Contains(saudeInimigo))
                inimigosNaArea.Add(saudeInimigo);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isServerMode) return;
        if (other.CompareTag("Enemy"))
        {
            EnemyHealthSystem saudeInimigo = other.GetComponent<EnemyHealthSystem>();
            if (saudeInimigo != null)
                inimigosNaArea.Remove(saudeInimigo);
        }
    }

    private IEnumerator CicloEspinhos()
    {
        while (true)
        {
            yield return new WaitForSeconds(tempoRecarga);

            if (animatorEspinhos != null) animatorEspinhos.SetTrigger("Ativar");
            AplicarDano();

            yield return new WaitForSeconds(tempoAtivo);

            if (animatorEspinhos != null) animatorEspinhos.SetTrigger("Desativar");
        }
    }

    private void AplicarDano()
    {
        for (int i = inimigosNaArea.Count - 1; i >= 0; i--)
        {
            if (inimigosNaArea[i] == null) { inimigosNaArea.RemoveAt(i); continue; }
            inimigosNaArea[i].TakeDamage(dano);
        }
    }
}