using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Fogueira: Armadilha que cura Jogadores e Torres próximas por segundo.
/// Cura roda apenas no servidor (ou localmente offline).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class Fogueira : TrapLogicBase
{
    public float curaPorSegundo = 5f;
    public float taxaDeCura = 1.0f;

    private List<Collider> alvosNaArea = new List<Collider>();
    private float tempoDesdeUltimaCura = 0f;
    private bool isServerMode = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isServerMode = IsServer;
        if (isServerMode) GetComponent<SphereCollider>().isTrigger = true;
    }

    void Start()
    {
        // Funciona offline E como host (IsServer = true em ambos)
        isServerMode = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        var sc = GetComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 5f; // raio da area de cura (configuravel no Inspector depois)
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Tower"))
        {
            if (!alvosNaArea.Contains(other)) alvosNaArea.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        alvosNaArea.Remove(other);
    }

    void Update()
    {
        if (!isServerMode) return;

        tempoDesdeUltimaCura += Time.deltaTime;
        if (tempoDesdeUltimaCura >= taxaDeCura)
        {
            CurarAlvos();
            tempoDesdeUltimaCura = 0f;
        }
    }

    private void CurarAlvos()
    {
        float curaTick = curaPorSegundo * taxaDeCura;

        for (int i = alvosNaArea.Count - 1; i >= 0; i--)
        {
            Collider alvo = alvosNaArea[i];
            if (alvo == null) { alvosNaArea.RemoveAt(i); continue; }

            if (alvo.CompareTag("Player"))
            {
                PlayerHealthSystem saude = alvo.GetComponent<PlayerHealthSystem>();
                if (saude != null) saude.Heal(curaTick);
            }
            else if (alvo.CompareTag("Tower"))
            {
                TowerController torre = alvo.GetComponent<TowerController>();
                if (torre != null) torre.Heal(curaTick);
            }
        }
    }
}