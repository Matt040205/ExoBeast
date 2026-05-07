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

        [Header("Configurações de Tempo")]
        [SerializeField] private float minimumDisplayTime = 2.5f; // Tempo para dar chance dos personagens materializarem
        [SerializeField] private float fadeOutDuration = 0.5f;
        
        private float timeShown;
        private CanvasGroup canvasGroup;
        public bool IsVisible => loadingPanel != null && loadingPanel.activeInHierarchy;

        private void Awake()
        {
            // Padrão Singleton persistente
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null); // DontDestroyOnLoad só funciona em objetos na raiz!
                DontDestroyOnLoad(gameObject); // Garante que a tela sobreviva às trocas de cena
            }
            else
            {
                Destroy(gameObject); // Se já existir uma tela, destrói essa duplicada
                return;
            }

            // Tenta pegar ou adicionar o CanvasGroup para fazer o Fade
            if (loadingPanel != null)
            {
                canvasGroup = loadingPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = loadingPanel.AddComponent<CanvasGroup>();
            }

            // Oculta a tela ao iniciar por padrão
            HideInstant();
        }

        private float currentVisualProgress = 0f;
        private Coroutine progressCoroutine;

        public void Show()
        {
            Debug.Log("[LoadingScreenUI] Show() chamado.");
            StopAllCoroutines();
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.blocksRaycasts = true;
                }
            }

            timeShown = Time.realtimeSinceStartup;
            currentVisualProgress = 0f;
            UpdateProgress(0f);

            // Anima até 90% durante o tempo mínimo esperado (ou trava nos 90% esperando a rede)
            progressCoroutine = StartCoroutine(AnimateProgressTo(0.9f, minimumDisplayTime));
        }

        private System.Collections.IEnumerator AnimateProgressTo(float target, float duration)
        {
            float start = currentVisualProgress;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                currentVisualProgress = Mathf.Lerp(start, target, elapsed / duration);
                UpdateVisuals(currentVisualProgress);
                yield return null;
            }
            currentVisualProgress = target;
            UpdateVisuals(currentVisualProgress);
        }

        public void Hide()
        {
            if (progressCoroutine != null)
                StopCoroutine(progressCoroutine);

            float timeActive = Time.realtimeSinceStartup - timeShown;
            Debug.Log($"[LoadingScreenUI] Hide() chamado. Tempo ativo: {timeActive}s (Mínimo: {minimumDisplayTime}s)");
            
            if (timeActive < minimumDisplayTime)
            {
                float timeToWait = minimumDisplayTime - timeActive;
                Debug.Log($"[LoadingScreenUI] Completando os 10% finais em {timeToWait}s...");
                StartCoroutine(FinishProgressAndHide(timeToWait));
            }
            else
            {
                Debug.Log("[LoadingScreenUI] Tempo mínimo já passou. Completando 100% e ocultando.");
                currentVisualProgress = 1f;
                UpdateVisuals(1f);
                StartCoroutine(FadeOutRoutine());
            }
        }

        public void ForceHide()
        {
            StopAllCoroutines();
            progressCoroutine = null;
            currentVisualProgress = 0f;
            UpdateVisuals(0f);
            HideInstant();
        }

        private System.Collections.IEnumerator FinishProgressAndHide(float delay)
        {
            float start = currentVisualProgress;
            float elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.unscaledDeltaTime;
                currentVisualProgress = Mathf.Lerp(start, 1f, elapsed / delay);
                UpdateVisuals(currentVisualProgress);
                yield return null;
            }
            
            currentVisualProgress = 1f;
            UpdateVisuals(1f);
            
            Debug.Log("[LoadingScreenUI] Fim da espera. Iniciando Fade Out.");
            yield return StartCoroutine(FadeOutRoutine());
        }

        private System.Collections.IEnumerator FadeOutRoutine()
        {
            if (canvasGroup != null)
            {
                float startAlpha = canvasGroup.alpha;
                float elapsed = 0f;

                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                    yield return null;
                }
            }
            
            HideInstant();
        }

        private void HideInstant()
        {
            Debug.Log("[LoadingScreenUI] Tela de carregamento oculta (HideInstant).");
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }

            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        /// <summary>
        /// Define diretamente o progresso (Usado apenas no reset)
        /// </summary>
        public void UpdateProgress(float progress)
        {
            currentVisualProgress = progress;
            UpdateVisuals(progress);
        }

        /// <summary>
        /// Atualiza visualmente o progresso na UI.
        /// </summary>
        private void UpdateVisuals(float progress)
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
