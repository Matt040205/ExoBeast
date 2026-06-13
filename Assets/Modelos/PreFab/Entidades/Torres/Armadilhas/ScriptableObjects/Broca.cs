using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Broca: Armadilha que gera Geoditas periodicamente.
/// Toda lógica de currency roda apenas no servidor.
/// </summary>
public class Broca : TrapLogicBase
{
    public int geodidasPorCiclo = 2; 
    public float tempoPorCiclo = 10f; 

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        StartCoroutine(GerarGeodidas());
    }

    // Fallback para modo offline (sem NGO ativo)
    void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            StartCoroutine(GerarGeodidas());
        }
    }

    private System.Collections.IEnumerator GerarGeodidas()
    {
        while (true)
        {
            yield return new WaitForSeconds(tempoPorCiclo);

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.AddCurrency(geodidasPorCiclo, CurrencyType.Geodites);
            }
        }
    }
}