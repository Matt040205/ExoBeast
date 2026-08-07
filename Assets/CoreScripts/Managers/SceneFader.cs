using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ExoBeasts.Managers
{
    /// <summary>
    /// Gerenciador de transição Fade In / Fade Out entre cenas.
    /// Cria dinamicamente uma tela preta overlay persistente (DontDestroyOnLoad)
    /// com a maior prioridade de renderização (SortingOrder 9999).
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        public static SceneFader Instance { get; private set; }

        [Header("Configurações de Fade")]
        [SerializeField] private float defaultFadeOutDuration = 0.5f;  // Escurecer a tela
        [SerializeField] private float defaultFadeInDuration = 1.3f;  // Clarear a tela na nova cena
        [SerializeField] private Color fadeColor = Color.black;

        private CanvasGroup _canvasGroup;
        private Image _fadeImage;
        private bool _isFading;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            CreateFadeUIIfNeeded();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        /// <summary>
        /// Cria dinamicamente a UI de Fade se não existir.
        /// </summary>
        private void CreateFadeUIIfNeeded()
        {
            if (_canvasGroup != null) return;

            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("FadeCanvas");
                canvasGO.transform.SetParent(transform, false);

                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999; // Maior prioridade da UI

                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasGO.AddComponent<GraphicRaycaster>();
            }

            if (_fadeImage == null)
            {
                _fadeImage = GetComponentInChildren<Image>();
                if (_fadeImage == null && canvas != null)
                {
                    GameObject imgGO = new GameObject("FadeImage");
                    imgGO.transform.SetParent(canvas.transform, false);

                    RectTransform rt = imgGO.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;

                    _fadeImage = imgGO.AddComponent<Image>();
                    _fadeImage.color = fadeColor;
                }
            }

            _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null && _fadeImage != null)
            {
                _canvasGroup = _fadeImage.gameObject.AddComponent<CanvasGroup>();
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateFadeUIIfNeeded();
            if (_canvasGroup != null)
            {
                // Mantém 100% preto ao carregar a nova cena e inicia o Fade In gradual
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;

                if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = StartCoroutine(FadeInRoutine(defaultFadeInDuration));
            }
        }

        /// <summary>
        /// Escurece a tela (Fade Out para preto).
        /// </summary>
        public IEnumerator FadeOutRoutine(float duration = -1f)
        {
            if (duration <= 0f) duration = defaultFadeOutDuration;
            CreateFadeUIIfNeeded();

            _isFading = true;
            _canvasGroup.blocksRaycasts = true;

            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _isFading = false;
        }

        /// <summary>
        /// Clareia a tela (Fade In de preto para transparente na nova cena).
        /// </summary>
        public IEnumerator FadeInRoutine(float duration = -1f)
        {
            if (duration <= 0f) duration = defaultFadeInDuration;
            CreateFadeUIIfNeeded();

            _isFading = true;
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            // Espera 1 frame para ignorar o pico de tempo (lag spike) do carregamento da cena pelo Unity
            yield return null;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _isFading = false;
        }

        /// <summary>
        /// Carrega uma cena executando Fade Out -> Carregar Cena -> Fade In.
        /// </summary>
        public void FadeToScene(string sceneName, float fadeOutDuration = -1f, float fadeInDuration = -1f)
        {
            if (_isFading) return;
            StartCoroutine(FadeAndLoadRoutine(sceneName, fadeOutDuration, fadeInDuration));
        }

        private IEnumerator FadeAndLoadRoutine(string sceneName, float fadeOutDuration, float fadeInDuration)
        {
            yield return StartCoroutine(FadeOutRoutine(fadeOutDuration));
            GameModeManager.LoadSceneSafe(sceneName);
        }
    }
}
