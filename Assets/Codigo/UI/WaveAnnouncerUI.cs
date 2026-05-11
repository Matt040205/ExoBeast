using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// UI que exibe o anuncio de Fase de Preparacao antes de cada onda.
/// Mostra o titulo, a lista de inimigos que virao, e um contador regressivo.
/// Deve ser arrastada nos campos do HordeManager no Inspector.
/// </summary>
public class WaveAnnouncerUI : MonoBehaviour
{
    public static WaveAnnouncerUI Instance { get; private set; }

    [Header("Painel Principal")]
    [SerializeField] private GameObject announcerPanel;

    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI titleText;       // "Fase de Preparacao"
    [SerializeField] private TextMeshProUGUI enemyListText;   // "2x Aranha, 1x Aguia..."
    [SerializeField] private TextMeshProUGUI countdownText;   // "45"

    private Coroutine countdownCoroutine;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        Hide();
    }

    /// <summary>
    /// Mostra o painel de anuncio com titulo, lista de inimigos e inicia o contador.
    /// </summary>
    /// <param name="titulo">Ex: "Fase de Preparacao" ou "Proxima Onda"</param>
    /// <param name="listaInimigos">Ex: "2x Aranha, 1x Aguia, 3x Escorpiao"</param>
    /// <param name="duracaoSegundos">Tempo total do contador regressivo</param>
    public void ShowAnnouncement(string titulo, string listaInimigos, float duracaoSegundos)
    {
        if (announcerPanel != null)
            announcerPanel.SetActive(true);

        if (titleText != null)
            titleText.text = titulo;

        if (enemyListText != null)
            enemyListText.text = listaInimigos;

        // Inicia contagem regressiva
        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);

        countdownCoroutine = StartCoroutine(CountdownRoutine(duracaoSegundos));
    }

    /// <summary>
    /// Esconde o painel de anuncio.
    /// </summary>
    public void Hide()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (announcerPanel != null)
            announcerPanel.SetActive(false);
    }

    private IEnumerator CountdownRoutine(float totalSeconds)
    {
        float remaining = totalSeconds;

        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(remaining).ToString();

            yield return null;
            remaining -= Time.deltaTime;
        }

        if (countdownText != null)
            countdownText.text = "0";

        countdownCoroutine = null;
    }
}
