using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // Adicionado o namespace do novo Input System
using UnityEngine.VFX;

public class DissControl : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMesh;
    public VisualEffect visualEffect;
    public float dissolveRate = 0.0125f;
    public float refreshRate = 0.025f;

    private Material[] skinedmat;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Corrigido de "==" para "!=" para garantir que o renderer foi assinalado
        if (skinnedMesh != null)
            skinedmat = skinnedMesh.materials;
    }

    // Update is called once per frame
    void Update()
    {
        // Verificação simples e direta para o Novo Input System
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            StartCoroutine(DissolveCorr());
        }
    }

    IEnumerator DissolveCorr()
    {

        if(visualEffect != null)
        {
            visualEffect.Play();
        }
        // Adicionado um check básico para evitar erros caso skinedmat seja nulo
        if (skinedmat != null && skinedmat.Length > 0)
        {
            float counter = 0;
            while (skinedmat[0].GetFloat("_dissolveamount") < 1)
            {
                counter += dissolveRate;
                for (int i = 0; i < skinedmat.Length; i++)
                {
                    skinedmat[i].SetFloat("_dissolveamount", counter);
                }
                yield return new WaitForSeconds(refreshRate);
            }
        }
    }
}