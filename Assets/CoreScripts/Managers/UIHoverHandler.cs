using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ── UIHoverHandler ───────────────────────────────────────
/// Detecta quando o ponteiro do mouse entra e sai de um elemento da UI.
/// Dispara eventos para atualizar o tooltip dinâmico único do Canvas.
/// ─────────────────────────────────────────────────────────
/// </summary>
public class UIHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action onPointerEnterAction;
    public Action onPointerExitAction;

    public void OnPointerEnter(PointerEventData eventData)
    {
        onPointerEnterAction?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onPointerExitAction?.Invoke();
    }
}
