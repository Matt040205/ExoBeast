using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ParticleSystem hoverParticles;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverParticles != null)
            hoverParticles.Play();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverParticles != null)
            hoverParticles.Stop();
    }
}