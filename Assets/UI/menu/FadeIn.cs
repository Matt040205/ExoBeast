using UnityEngine;
using UnityEngine.UI;

public class FadeIn : MonoBehaviour
{
    public float fadeDuration = 2.0f;
    private Image image;
    private float timer = 0f;

    void Start()
    {
        image = GetComponent<Image>();
        if (image != null)
        {
            Color c = image.color;
            c.a = 0f;
            image.color = c;
        }
    }

    void Update()
    {
        if (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Clamp01(timer / fadeDuration);

            if (image != null)
            {
                Color c = image.color;
                c.a = alpha;
                image.color = c;
            }
        }
    }
}