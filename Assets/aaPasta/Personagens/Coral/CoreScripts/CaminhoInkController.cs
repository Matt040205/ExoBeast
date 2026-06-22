using UnityEngine;
using UnityEngine.VFX;

public class CaminhoInkController : MonoBehaviour
{
    [Tooltip("Se a esfera for um GameObject separado dentro do Prefab, arraste-a aqui.")]
    public GameObject sphereObject;

    [Tooltip("Se a esfera for controlada por um Bool dentro do VFX Graph, arraste o VisualEffect aqui.")]
    public VisualEffect vfx;
    
    [Tooltip("Nome da propriedade Bool no VFX Graph que liga/desliga a esfera.")]
    public string vfxSphereProperty = "ShowSphere";

    public void SetSphereActive(bool active)
    {
        if (sphereObject != null)
            sphereObject.SetActive(active);

        if (vfx != null && vfx.HasBool(vfxSphereProperty))
            vfx.SetBool(vfxSphereProperty, active);
    }
}
