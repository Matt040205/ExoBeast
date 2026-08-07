using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Efeito de hover nos botões do menu:
/// - Desloca o botão para a esquerda ao passar o mouse (hover slide)
/// - Aciona/para partículas existentes
/// - Coordena com MenuButtonFloat e MenuButtonsEntrance para nunca ficar preso de lado
/// </summary>
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Partículas (existentes)")]
    public ParticleSystem hoverParticles;

    [Header("Hover Slide")]
    [Tooltip("Distância de deslocamento horizontal ao fazer hover (pixels). Negativo = esquerda.")]
    public float slideDistanceX = -18f;

    [Tooltip("Duração da animação de entrada do hover.")]
    public float slideInDuration = 0.22f;

    [Tooltip("Duração da animação de retorno do hover.")]
    public float slideOutDuration = 0.35f;

    public Ease slideInEase = Ease.OutSine;
    public Ease slideOutEase = Ease.InOutSine;

    private RectTransform _rt;
    private Vector2 _basePosition;
    private Tween _slideTween;
    private MenuButtonsEntrance _entranceComp;
    private bool _isHovered;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _basePosition = _rt.anchoredPosition;

        if (transform.parent != null)
        {
            _entranceComp = transform.parent.GetComponent<MenuButtonsEntrance>();
        }
    }

    private void OnEnable()
    {
        ResetHoverState();
    }

    private void OnDisable()
    {
        ResetHoverState();
    }

    /// <summary>
    /// Reseta o estado do hover e retorna o botão para sua posição base unshifted.
    /// Evita que o botão fique preso de lado ao mudar de cena ou reativar.
    /// </summary>
    public void ResetHoverState()
    {
        _isHovered = false;
        _slideTween?.Kill();

        if (_rt != null)
        {
            Vector2 basePos = GetTrueBasePosition();
            _rt.anchoredPosition = new Vector2(basePos.x, _rt.anchoredPosition.y);
        }

        if (hoverParticles != null)
            hoverParticles.Stop();
    }

    private Vector2 GetTrueBasePosition()
    {
        if (_entranceComp != null && _entranceComp.TryGetOriginalPosition(_rt, out Vector2 orig))
        {
            return orig;
        }
        return _basePosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isHovered) return;
        _isHovered = true;

        Vector2 basePos = GetTrueBasePosition();

        if (hoverParticles != null)
            hoverParticles.Play();

        _slideTween?.Kill();
        _slideTween = DOTween.To(
            () => _rt.anchoredPosition.x,
            x => _rt.anchoredPosition = new Vector2(x, _rt.anchoredPosition.y),
            basePos.x + slideDistanceX,
            slideInDuration
        ).SetEase(slideInEase).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isHovered) return;
        _isHovered = false;

        Vector2 basePos = GetTrueBasePosition();

        if (hoverParticles != null)
            hoverParticles.Stop();

        _slideTween?.Kill();
        _slideTween = DOTween.To(
            () => _rt.anchoredPosition.x,
            x => _rt.anchoredPosition = new Vector2(x, _rt.anchoredPosition.y),
            basePos.x,
            slideOutDuration
        ).SetEase(slideOutEase).SetUpdate(true);
    }

    private void OnDestroy()
    {
        _slideTween?.Kill();
    }
}
