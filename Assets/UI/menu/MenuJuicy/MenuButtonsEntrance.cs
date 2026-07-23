using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Anima os botões principais do menu deslizando da esquerda para a posição original ao iniciar a cena.
/// Adicione este componente no container pai dos botões (ex: "Buttons").
/// Os filhos diretos do container são animados em sequência.
///
/// NOTA IMPORTANTE: Desativa temporariamente ou permanentemente o LayoutGroup (Vertical/Horizontal/Grid)
/// do container para permitir que os RectTransforms dos botões sejam movidos livremente pelo DOTween.
/// Também desativa automaticamente o SequentialFade se encontrado, para evitar conflito de alpha.
/// </summary>
public class MenuButtonsEntrance : MonoBehaviour
{
    [Header("Configuração de Entrada")]
    [Tooltip("Distância de onde os botões partem (em pixels). Negativo = vem da esquerda para a direita.")]
    public float slideFromOffsetX = -1200f;

    [Tooltip("Duração da animação de entrada de cada botão.")]
    public float durationPerButton = 0.6f;

    [Tooltip("Delay entre a entrada de cada botão consecutivo.")]
    public float delayBetweenButtons = 0.12f;

    [Tooltip("Delay inicial antes do primeiro botão começar a entrar.")]
    public float initialDelay = 0.2f;

    [Tooltip("Easing da animação de entrada.")]
    public Ease enterEase = Ease.OutCubic;

    // Posições originais de cada botão para referência de outros sistemas (float, hover)
    private readonly List<RectTransform> _buttonRects = new List<RectTransform>();
    private readonly List<Vector2> _originalPositions = new List<Vector2>();

    private void Start()
    {
        // Desativa IMEDIATAMENTE o SequentialFade para evitar conflito de alpha nos botões
        DisableSequentialFade();

        StartCoroutine(SetupAndPlayEntrance());
    }

    /// <summary>
    /// Procura e desativa o SequentialFade no mesmo GameObject, no pai, ou em qualquer
    /// ancestral até o Canvas, para evitar que ele zere o alpha dos botões.
    /// Também reseta o alpha de todos os filhos para 1.
    /// </summary>
    private void DisableSequentialFade()
    {
        // Procura SequentialFade nos ancestrais (ele pode estar no mesmo objeto ou no Canvas/Manager)
        SequentialFade[] fades = GetComponentsInParent<SequentialFade>(true);
        foreach (var fade in fades)
        {
            if (fade != null)
            {
                fade.StopAllCoroutines();
                fade.enabled = false;
                Debug.Log($"[MenuButtonsEntrance] Desativou SequentialFade em '{fade.gameObject.name}' para evitar conflito de alpha.");
            }
        }

        // Também procura no pai (caso esteja em um irmão do Canvas)
        if (transform.parent != null)
        {
            SequentialFade parentFade = transform.parent.GetComponentInChildren<SequentialFade>(true);
            if (parentFade != null)
            {
                parentFade.StopAllCoroutines();
                parentFade.enabled = false;
                Debug.Log($"[MenuButtonsEntrance] Desativou SequentialFade (sibling) em '{parentFade.gameObject.name}'.");
            }
        }

        // Reseta o alpha de TODOS os gráficos dos filhos para 1 (desfaz o hideOnStart)
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            foreach (var graphic in child.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null)
                {
                    Color c = graphic.color;
                    c.a = 1f;
                    graphic.color = c;
                }
            }
        }
    }

    private IEnumerator SetupAndPlayEntrance()
    {
        // 1. Força a atualização do layout do Canvas para calcular as posições exatas dos botões
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        // 2. Se o container tiver um LayoutGroup (ex: VerticalLayoutGroup), desativa-o
        //    para que ele não trave a anchoredPosition dos botões durante os tweens!
        LayoutGroup layoutGroup = GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
            Debug.Log("[MenuButtonsEntrance] VerticalLayoutGroup desativado para permitir animação DOTween.");
        }

        // Também desativa ContentSizeFitter se existir
        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.enabled = false;
        }

        // 3. Salva as posições calculadas pelo layout
        _buttonRects.Clear();
        _originalPositions.Clear();

        foreach (Transform child in transform)
        {
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt != null && child.gameObject.activeSelf)
            {
                _buttonRects.Add(rt);
                _originalPositions.Add(rt.anchoredPosition);
            }
        }

        // 4. Desloca todos os botões para fora da tela (à esquerda)
        for (int i = 0; i < _buttonRects.Count; i++)
        {
            _buttonRects[i].anchoredPosition = _originalPositions[i] + new Vector2(slideFromOffsetX, 0f);
        }

        yield return new WaitForSeconds(initialDelay);

        // 5. Anima cada botão para a sua posição original
        for (int i = 0; i < _buttonRects.Count; i++)
        {
            int index = i; // captura para closure
            RectTransform rt = _buttonRects[index];
            Vector2 targetPos = _originalPositions[index];

            rt.DOAnchorPos(targetPos, durationPerButton)
              .SetEase(enterEase)
              .SetUpdate(true); // funciona mesmo com timeScale 0

            yield return new WaitForSeconds(delayBetweenButtons);
        }
    }

    /// <summary>
    /// Retorna a posição original (pré-entrada) de um botão pelo índice.
    /// Usado por MenuButtonFloat e ButtonHoverEffect como posição base.
    /// </summary>
    public bool TryGetOriginalPosition(RectTransform rt, out Vector2 originalPos)
    {
        int idx = _buttonRects.IndexOf(rt);
        if (idx >= 0 && idx < _originalPositions.Count)
        {
            originalPos = _originalPositions[idx];
            return true;
        }
        originalPos = rt.anchoredPosition;
        return false;
    }
}
