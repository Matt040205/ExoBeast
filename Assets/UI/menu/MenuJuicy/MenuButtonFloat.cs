using UnityEngine;
using DG.Tweening;

/// <summary>
/// Faz o botão flutuar levemente na vertical de forma contínua, criando um efeito "vivo".
/// Adicione este componente em cada botão individual do menu principal.
/// O float atua apenas no eixo Y; o eixo X é controlado por ButtonHoverEffect.
/// </summary>
public class MenuButtonFloat : MonoBehaviour
{
    [Header("Flutuação")]
    [Tooltip("Amplitude máxima do deslocamento vertical (em pixels).")]
    public float amplitude = 12f;

    [Tooltip("Duração de um ciclo completo de flutuação (subida + descida).")]
    public float cycleDuration = 1.0f;

    [Tooltip("Offset de fase em segundos, para desincronizar botões. " +
             "Deixe -1 para gerar aleatoriamente.")]
    public float phaseOffsetSeconds = -1f;

    private RectTransform _rt;
    private Vector2 _basePosition;
    private Tween _floatTween;
    private bool _initialized;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // A posição base é lida no Start para garantir que o MenuButtonsEntrance já definiu
        // a posição original corretamente.
        // Aguardamos um frame extra caso a entrada ainda esteja acontecendo.
        StartFloat();
    }

    private void StartFloat()
    {
        _basePosition = _rt.anchoredPosition;
        _initialized = true;

        float phase = phaseOffsetSeconds < 0f
            ? Random.Range(0f, cycleDuration)
            : phaseOffsetSeconds;

        // Inicia o tween de float com offset de fase usando um delay negativo via progresso inicial
        _floatTween = _rt.DOAnchorPosY(_basePosition.y + amplitude, cycleDuration * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);

        // Aplica o offset de fase avançando o tween
        if (phase > 0f)
        {
            float normalizedPhase = (phase % cycleDuration) / cycleDuration;
            _floatTween.fullPosition = normalizedPhase * (cycleDuration * 0.5f);
        }
    }

    /// <summary>
    /// Pausa o float (chamado pelo ButtonHoverEffect durante o hover, se necessário).
    /// </summary>
    public void PauseFloat()
    {
        _floatTween?.Pause();
    }

    /// <summary>
    /// Retoma o float depois de um hover.
    /// </summary>
    public void ResumeFloat()
    {
        _floatTween?.Play();
    }

    /// <summary>
    /// Atualiza a posição base (útil se o botão foi reposicionado por outra animação).
    /// </summary>
    public void SetBasePositionY(float newY)
    {
        if (!_initialized) return;
        _basePosition = new Vector2(_basePosition.x, newY);
        _floatTween?.Kill();

        _floatTween = _rt.DOAnchorPosY(_basePosition.y + amplitude, cycleDuration * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void OnDestroy()
    {
        _floatTween?.Kill();
    }

    private void OnDisable()
    {
        _floatTween?.Pause();
    }

    private void OnEnable()
    {
        if (_initialized)
            _floatTween?.Play();
    }
}
