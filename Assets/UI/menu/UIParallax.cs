using UnityEngine;
using UnityEngine.UI;

public class UIParallax : MonoBehaviour
{
    [Header("Configurações")]
    public Vector2 scrollSpeed = new Vector2(0.1f, 0f);
    public bool independenteDeTimeScale = true;

    private RawImage rawImage;
    private Rect uvRect;

    void Start()
    {
        rawImage = GetComponent<RawImage>();
        uvRect = rawImage.uvRect;
    }

    void Update()
    {
        float deltaTime = independenteDeTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
        
        uvRect.x += scrollSpeed.x * deltaTime;
        uvRect.y += scrollSpeed.y * deltaTime;
        
        // Mantém os valores UV dentro do range [0, 1]
        uvRect.x %= 1;
        uvRect.y %= 1;
        
        rawImage.uvRect = uvRect;
    }
}