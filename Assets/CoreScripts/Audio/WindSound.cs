using UnityEngine;
using System.Collections;
using FMODUnity;

public class WindSound : MonoBehaviour
{
    [Header("Configuração do Som")]
    [EventRef]
    public string eventoVento = "event:/SFX/Vento";

    [Header("Configuração de Tempo (em segundos)")]
    [Tooltip("O tempo MÍNIMO que o script vai esperar antes de TENTAR tocar o som.")]
    public float minIntervalo = 10f;
    [Tooltip("O tempo MÁXIMO que o script vai esperar antes de TENTAR tocar o som.")]
    public float maxIntervalo = 25f;

    [Header("Configuração da Chance")]
    [Tooltip("A chance (em porcentagem) que o som tem de tocar a cada intervalo.")]
    [Range(0, 100)]
    public float chancePercentual = 30f;
    
    void Start()
    {
        if (string.IsNullOrEmpty(eventoVento))
        {
            Debug.LogError("Evento 'Vento' do FMOD não foi definido no WindSound.cs", this);
            return;
        }

        StartCoroutine(WindLoopCoroutine());
    }

    private IEnumerator WindLoopCoroutine()
    {
        while (true)
        {
            float tempoDeEspera = Random.Range(minIntervalo, maxIntervalo);
            yield return new WaitForSeconds(tempoDeEspera);

            float chanceRoll = Random.Range(0f, 100f);

            if (chanceRoll <= chancePercentual)
            {
                RuntimeManager.PlayOneShot(eventoVento);
            }
        }
    }
}