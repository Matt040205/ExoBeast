using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// ── UIHoverHandler ───────────────────────────────────────
/// Detecta quando o ponteiro do mouse entra e sai de um elemento da UI.
/// Suporta configuração direta no Inspector (Ativar GameObject / UnityEvents) e via Código (Actions).
/// ─────────────────────────────────────────────────────────
/// </summary>
public class UIHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Ação Rápida no Inspector (Opcional)")]
    [Tooltip("Arraste o GameObject que deve ser LIGADO quando o mouse passar por cima deste elemento e DESLIGADO ao sair.")]
    public GameObject objetoParaAtivarNoHover;

    [Header("Eventos no Inspector")]
    [Tooltip("Disparado quando o mouse entra sobre este elemento da UI.")]
    public UnityEvent onHoverEnter;

    [Tooltip("Disparado quando o mouse sai deste elemento da UI.")]
    public UnityEvent onHoverExit;

    // Callbacks via código C# (repassados dinamicamente por Managers)
    public Action onPointerEnterAction;
    public Action onPointerExitAction;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (objetoParaAtivarNoHover != null)
            objetoParaAtivarNoHover.SetActive(true);

        onHoverEnter?.Invoke();
        onPointerEnterAction?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (objetoParaAtivarNoHover != null)
            objetoParaAtivarNoHover.SetActive(false);

        onHoverExit?.Invoke();
        onPointerExitAction?.Invoke();
    }
}
