using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class SequentialFade : MonoBehaviour
{
    public List<GameObject> elements;
    public float delayBetweenElements = 0.5f;
    public float fadeDuration = 1.0f;
    public bool hideOnStart = true; // Nova vari�vel

void Start()
    {
        if (hideOnStart)
        {
            // Inicializa todos os elementos com alpha 0
            foreach (GameObject element in elements)
            {
                if (element != null) SetAlpha(element, 0f);
            }
        }

        StartCoroutine(FadeInSequence());
    }

void SetAlpha(GameObject obj, float alpha)
    {
        if (obj == null) return;
        // Para imagens
        Image img = obj.GetComponent<Image>();
        if (img != null)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }

        // Para textos padrão
        Text txt = obj.GetComponent<Text>();
        if (txt != null)
        {
            Color c = txt.color;
            c.a = alpha;
            txt.color = c;
        }

        // Para TextMeshPro
        TMP_Text tmp = obj.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            Color c = tmp.color;
            c.a = alpha;
            tmp.color = c;
        }
    }

    IEnumerator FadeInSequence()
    {
        foreach (GameObject element in elements)
        {
            if (element == null) continue;
            StartCoroutine(FadeElement(element));
            yield return new WaitForSeconds(delayBetweenElements);
        }
    }

    IEnumerator FadeElement(GameObject element)
    {
        if (element == null) yield break;

        float elapsed = 0f;
        List<Graphic> graphics = new List<Graphic>();

        // Coleta todos os componentes grficos
        graphics.AddRange(element.GetComponentsInChildren<Image>());
        graphics.AddRange(element.GetComponentsInChildren<Text>());
        graphics.AddRange(element.GetComponentsInChildren<TMP_Text>());

        // Fade in
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeDuration);

            foreach (Graphic graphic in graphics)
            {
                if (graphic != null)
                {
                    Color c = graphic.color;
                    c.a = alpha;
                    graphic.color = c;
                }
            }
            yield return null;
        }

        // Garante alpha final = 1
        foreach (Graphic graphic in graphics)
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