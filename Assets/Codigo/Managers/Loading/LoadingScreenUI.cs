using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ExoBeasts.Managers.Loading
{
    public class LoadingScreenUI : MonoBehaviour
    {
        public static LoadingScreenUI Instance { get; private set; }

        [Header("Configurações Visuais")]
        [SerializeField] private GameObject loadingPanel; // O painel principal que cobre a tela
        [SerializeField] private Slider progressBar; // Opcional: Barra de progresso (0 a 1)
        [SerializeField] private Image progressFillImage; // Opcional: Imagem preenchível (Fill Amount)
        [SerializeField] private TextMeshProUGUI progressText; // Opcional: Texto de progresso ("50%")

        private void Awake()
        {
            // Padrão Singleton persistente
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Garante que a tela sobreviva às trocas de cena
            }
            else
            {
                Destroy(gameObject); // Se já existir uma tela, destrói essa duplicada
                return;
            }

            // Oculta a tela ao iniciar por padrão (caso tenha esquecido ligada no Editor)
            Hide();
        }

        /// <summary>
        /// Mostra a tela de carregamento e reseta os visuais de progresso.
        /// </summary>
        public void Show()
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(true);

            UpdateProgress(0f);
        }

        /// <summary>
        /// Oculta a tela de carregamento.
        /// </summary>
        public void Hide()
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        /// <summary>
        /// Atualiza visualmente o progresso na UI.
        /// </summary>
        /// <param name="progress">Valor normalizado de 0.0 a 1.0</param>
        public void UpdateProgress(float progress)
        {
            // Atualiza Slider (se existir)
            if (progressBar != null)
                progressBar.value = progress;

            // Atualiza Fill Image (se existir em vez de Slider)
            if (progressFillImage != null)
                progressFillImage.fillAmount = progress;

            // Atualiza Texto (se existir)
            if (progressText != null)
            {
                int percentage = Mathf.RoundToInt(progress * 100f);
                progressText.text = $"Carregando... {percentage}%";
            }
        }
    }
}
