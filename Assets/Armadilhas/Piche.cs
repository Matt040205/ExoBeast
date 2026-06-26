using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

[RequireComponent(typeof(BoxCollider))]
public class Piche : TrapLogicBase
{
    public float percentualDesaceleracao = 0.5f;
    public float multiplicadorDanoVulneravel = 1.5f;
    public float duracaoVulneravelAposSair = 3f;

    private Dictionary<EnemyController, Coroutine> inimigosAfetados = new Dictionary<EnemyController, Coroutine>();
    private bool isServerMode = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isServerMode = IsServer;
    }

    void Start()
    {
        isServerMode = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        var bc = GetComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(3f, 0.5f, 3f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServerMode) return;
        if (!other.CompareTag("Enemy")) return;

        EnemyController inimigo = other.GetComponent<EnemyController>();
        EnemyHealthSystem saude = other.GetComponent<EnemyHealthSystem>();
        if (inimigo == null || saude == null) return;

        if (inimigosAfetados.ContainsKey(inimigo))
        {
            StopCoroutine(inimigosAfetados[inimigo]);
            inimigosAfetados.Remove(inimigo);
        }

        inimigo.AplicarDesaceleracao(percentualDesaceleracao);
        saude.AplicarVulnerabilidade(multiplicadorDanoVulneravel);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isServerMode) return;
        if (!other.CompareTag("Enemy")) return;

        EnemyController inimigo = other.GetComponent<EnemyController>();
        EnemyHealthSystem saude = other.GetComponent<EnemyHealthSystem>();
        if (inimigo == null || saude == null) return;

        inimigo.RemoverDesaceleracao();
        Coroutine r = StartCoroutine(ManterVulnerabilidadeTemporaria(saude));
        inimigosAfetados[inimigo] = r;
    }

    private IEnumerator ManterVulnerabilidadeTemporaria(EnemyHealthSystem saude)
    {
        if (saude == null) yield break;
        yield return new WaitForSeconds(duracaoVulneravelAposSair);
        if (saude != null) saude.RemoverVulnerabilidade();
    }
}
