using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Gerencia a navegação entre painéis do menu com pilha de histórico e slide animado de TELA CHEIA (Full Page Push).
/// O Menu Principal inteiro (Background, Logo e Botões) se move em conjunto como uma página sólida,
/// sem sobrepor e sem deixar o fundo parado.
/// </summary>
public class MenuTabSlider : MonoBehaviour
{
    public static MenuTabSlider Instance { get; private set; }

    // ──────────────────────────────────────────────
    // Tipos
    // ──────────────────────────────────────────────

    [System.Serializable]
    public class PanelEntry
    {
        [Tooltip("ID único da aba (ex: 'Options', 'Credits').")]
        public string id;

        [Tooltip("O RectTransform da aba.")]
        public RectTransform panel;

        [Tooltip("Marque se deseja especificar manualmente a posição central na tela.")]
        public bool overrideCenterPosition;

        [Tooltip("Posição central na tela quando visível. Usado se overrideCenterPosition for true.")]
        public Vector2 customCenterPosition = Vector2.zero;
    }

    // ──────────────────────────────────────────────
    // Campos do Inspector
    // ──────────────────────────────────────────────

    [Header("Referências")]
    [Tooltip("O container dos botões principais (GameObject 'Buttons').")]
    public RectTransform buttonsContainer;

    [Tooltip("Elementos visuais adicionais do Menu Principal que devem deslizar junto com os botões (ex: Background, Logo). Se vazio, é detectado automaticamente.")]
    public List<RectTransform> mainMenuExtraElements = new List<RectTransform>();

    [Tooltip("Lista de todos os painéis de aba registrados.")]
    public List<PanelEntry> registeredPanels = new List<PanelEntry>();

    [Header("Configuração de Transição")]
    [Tooltip("Distância horizontal do slide (pixels). Padrão 1920 (largura Full HD).")]
    public float slideDistance = 1920f;

    [Tooltip("Duração da transição.")]
    public float transitionDuration = 0.42f;

    [Tooltip("Easing unificado da transição de tela cheia.")]
    public Ease transitionEase = Ease.OutCubic;

    // ──────────────────────────────────────────────
    // Estado interno
    // ──────────────────────────────────────────────

    private readonly Stack<RectTransform> _history = new Stack<RectTransform>();
    private RectTransform _currentPanel;
    private readonly Dictionary<RectTransform, Vector2> _onScreenPositions
        = new Dictionary<RectTransform, Vector2>();

    private bool _isTransitioning;

    // ──────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 1. Salva a posição on-screen do container dos botões
        if (buttonsContainer != null)
        {
            _onScreenPositions[buttonsContainer] = buttonsContainer.anchoredPosition;

            // Detecta automaticamente outros elementos do Menu Principal (Background, Logo) sob o Canvas
            if (mainMenuExtraElements.Count == 0 && buttonsContainer.parent != null)
            {
                foreach (Transform child in buttonsContainer.parent)
                {
                    RectTransform childRT = child.GetComponent<RectTransform>();
                    if (childRT == null || childRT == buttonsContainer) continue;

                    // Oculta abas registradas
                    bool isRegisteredPanel = false;
                    foreach (var entry in registeredPanels)
                    {
                        if (entry.panel == childRT) { isRegisteredPanel = true; break; }
                    }
                    if (isRegisteredPanel) continue;

                    string nameLower = child.name.ToLower();
                    if (nameLower.Contains("exitconfirmation") || nameLower.Contains("pause") || nameLower.Contains("eventsystem") || nameLower.Contains("manager"))
                        continue;

                    // Salva elementos como parte da página do menu principal
                    mainMenuExtraElements.Add(childRT);
                }
            }
        }

        // Salva as posições on-screen de todos os elementos extras do menu principal
        foreach (var extra in mainMenuExtraElements)
        {
            if (extra != null && !_onScreenPositions.ContainsKey(extra))
            {
                _onScreenPositions[extra] = extra.anchoredPosition;
            }
        }

        // 2. Registra posições on-screen das abas e as esconde
        foreach (var entry in registeredPanels)
        {
            if (entry.panel == null) continue;

            // Desativa script antigo se existir
            ExitConfirmation legacyExitScript = entry.panel.GetComponent<ExitConfirmation>();
            if (legacyExitScript != null)
            {
                legacyExitScript.enabled = false;
            }

            Vector2 centerPos;

            if (entry.overrideCenterPosition)
            {
                centerPos = entry.customCenterPosition;
            }
            else
            {
                centerPos = entry.panel.anchoredPosition;

                // Se o painel na cena estava no fundo ou fora (ex: Y < -300 ou X descalibrado),
                // ajusta para o centro exato (0,0)
                if (centerPos.y < -300f || Mathf.Abs(centerPos.x) > 500f)
                {
                    centerPos = Vector2.zero;
                }
            }

            _onScreenPositions[entry.panel] = centerPos;
            entry.panel.gameObject.SetActive(false);
        }

        // Menu principal começa ativo
        SetMainMenuActive(true);
        _currentPanel = null;
    }

    // ──────────────────────────────────────────────
    // API Pública
    // ──────────────────────────────────────────────

    public void NavigateTo(string panelId)
    {
        RectTransform target = FindPanelById(panelId);
        if (target == null)
        {
            Debug.LogWarning($"[MenuTabSlider] Painel com ID '{panelId}' não encontrado.");
            return;
        }
        NavigateTo(target);
    }

    public void NavigateTo(RectTransform targetPanel)
    {
        if (_isTransitioning) return;
        if (targetPanel == null) return;
        if (targetPanel == _currentPanel) return;

        StartCoroutine(DoNavigateTo(targetPanel));
    }

    public void NavigateTo(GameObject targetGO)
    {
        if (targetGO == null) return;
        NavigateTo(targetGO.GetComponent<RectTransform>());
    }

    public void Back()
    {
        if (_isTransitioning) return;
        if (_history.Count == 0 && _currentPanel == null) return;

        RectTransform previousPanel = _history.Count > 0 ? _history.Pop() : null;
        StartCoroutine(DoBack(previousPanel));
    }

    public bool CanGoBack() => _history.Count > 0 || _currentPanel != null;

    public void SlideToOptions() => NavigateTo("Options");
    public void SlideToCredits() => NavigateTo("Credits");
    public void SlideBack() => Back();

    // ──────────────────────────────────────────────
    // Coroutines de animação de Página Inteira (Full Page Push)
    // ──────────────────────────────────────────────

    private IEnumerator DoNavigateTo(RectTransform targetPanel)
    {
        _isTransitioning = true;

        RectTransform leaving = GetCurrentContainer();
        _history.Push(_currentPanel);

        float dist = GetEffectiveSlideDistance();
        Vector2 targetOnScreen = GetOnScreenPosition(targetPanel);

        // Se a página atual for o Menu Principal, movemos os botões E todos os elementos extras (Background, Logo)
        List<RectTransform> leavingGroup = GetGroupForContainer(leaving);

        // Posiciona o painel de destino fora da tela à DIREITA (+1920)
        targetPanel.anchoredPosition = new Vector2(targetOnScreen.x + dist, targetOnScreen.y);
        targetPanel.gameObject.SetActive(true);

        // Move a página que sai para a esquerda por exatamente 'dist' pixels
        foreach (var item in leavingGroup)
        {
            if (item == null) continue;
            Vector2 onScreen = GetOnScreenPosition(item);
            item.DOAnchorPosX(onScreen.x - dist, transitionDuration)
                .SetEase(transitionEase).SetUpdate(true);
        }

        // Move a nova página para a posição final de destino (0,0)
        yield return targetPanel.DOAnchorPos(targetOnScreen, transitionDuration)
                                .SetEase(transitionEase).SetUpdate(true)
                                .WaitForCompletion();

        // Desativa o grupo que saiu
        foreach (var item in leavingGroup)
        {
            if (item == null) continue;
            item.gameObject.SetActive(false);
        }

        _currentPanel = targetPanel;
        _isTransitioning = false;
    }

    private IEnumerator DoBack(RectTransform previousPanel)
    {
        _isTransitioning = true;

        RectTransform leaving = GetCurrentContainer();
        RectTransform entering = previousPanel != null ? previousPanel : buttonsContainer;

        float dist = GetEffectiveSlideDistance();
        Vector2 leavingOnScreen = GetOnScreenPosition(leaving);
        List<RectTransform> enteringGroup = GetGroupForContainer(entering);

        // Ativa a página de destino e a posiciona fora da tela à ESQUERDA (-1920)
        foreach (var item in enteringGroup)
        {
            if (item == null) continue;
            Vector2 onScreen = GetOnScreenPosition(item);
            item.anchoredPosition = new Vector2(onScreen.x - dist, onScreen.y);
            item.gameObject.SetActive(true);
        }

        // Move a página atual para a direita por exatamente 'dist' pixels
        leaving.DOAnchorPosX(leavingOnScreen.x + dist, transitionDuration)
               .SetEase(transitionEase).SetUpdate(true);

        // Move a página anterior para sua posição central de destino
        Tween lastTween = null;
        foreach (var item in enteringGroup)
        {
            if (item == null) continue;
            Vector2 onScreen = GetOnScreenPosition(item);
            lastTween = item.DOAnchorPos(onScreen, transitionDuration)
                            .SetEase(transitionEase).SetUpdate(true);
        }

        if (lastTween != null)
        {
            yield return lastTween.WaitForCompletion();
        }

        leaving.gameObject.SetActive(false);
        _currentPanel = previousPanel;
        _isTransitioning = false;
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private RectTransform GetCurrentContainer()
        => _currentPanel != null ? _currentPanel : buttonsContainer;

    private List<RectTransform> GetGroupForContainer(RectTransform container)
    {
        List<RectTransform> group = new List<RectTransform>();
        if (container == null) return group;

        group.Add(container);

        // Se for o menu principal (buttonsContainer), inclui também Background e Logo
        if (container == buttonsContainer)
        {
            foreach (var extra in mainMenuExtraElements)
            {
                if (extra != null && !group.Contains(extra))
                    group.Add(extra);
            }
        }
        return group;
    }

    private void SetMainMenuActive(bool active)
    {
        if (buttonsContainer != null)
            buttonsContainer.gameObject.SetActive(active);

        foreach (var extra in mainMenuExtraElements)
        {
            if (extra != null)
                extra.gameObject.SetActive(active);
        }
    }

    private Vector2 GetOnScreenPosition(RectTransform rt)
    {
        if (rt != null && _onScreenPositions.TryGetValue(rt, out Vector2 pos))
            return pos;
        return rt != null ? rt.anchoredPosition : Vector2.zero;
    }

    private float GetEffectiveSlideDistance()
    {
        if (slideDistance > 0f) return slideDistance;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            RectTransform canvasRT = canvas.GetComponent<RectTransform>();
            if (canvasRT != null && canvasRT.rect.width > 0f)
                return canvasRT.rect.width;
        }
        return 1920f;
    }

    private RectTransform FindPanelById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var entry in registeredPanels)
        {
            if (entry.id != null && entry.id.Equals(id, System.StringComparison.OrdinalIgnoreCase) && entry.panel != null)
                return entry.panel;
        }
        return null;
    }
}
