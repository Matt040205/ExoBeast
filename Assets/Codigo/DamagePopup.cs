using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshPro))]
public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Renderer textRenderer;
    private Color textColor;
    private Vector3 moveVector;
    private Transform camTransform;

    private float disappearTimer;
    private float lifetime;

    [Header("Configurações de Animação")]
    public float moveYSpeed = 5f;
    public float gravity = 8f;
    public float scatterForce = 2f;
    public float disappearTimerBase = 1f;
    public float disappearSpeed = 3f;

    [Header("Ordem de Renderização")]
    public int sortingOrder = 2005;

    [Header("Escala por Distância")]
    public bool useDistanceScaling = true;
    public float referenceDistance = 10f;
    public float maxScaleMultiplier = 4f;

    [Header("Efeito de Impacto")]
    public AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1.4f),
        new Keyframe(1f, 1f)
    );

    [Header("Quantidade de Estrelas")]
    public int starAmount = 3;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        textRenderer = GetComponent<Renderer>();

        if (textRenderer != null) textRenderer.sortingOrder = sortingOrder;
        if (Camera.main != null) camTransform = Camera.main.transform;
    }

    public void Setup(int damageAmount, bool isCriticalHit)
    {
        lifetime = 0f;
        disappearTimer = disappearTimerBase;
        transform.localScale = Vector3.zero;

        textMesh.SetText(damageAmount.ToString());

        textColor = textMesh.color;
        textColor.a = 1f;

        float randomX = Random.Range(-1f, 1f) * scatterForce;
        float randomZ = Random.Range(-1f, 1f) * scatterForce;
        moveVector = new Vector3(randomX, moveYSpeed, randomZ);

        int finalStarAmount = starAmount;

        if (isCriticalHit)
        {
            textMesh.fontSize = 8;
            textColor = Color.yellow;
            textMesh.fontSharedMaterial.SetColor("_UnderlayColor", Color.red);
            moveVector *= 1.5f;
            finalStarAmount += 2;
        }
        else
        {
            textMesh.fontSize = 5;
            textColor = Color.white;
            textMesh.fontSharedMaterial.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.5f));
        }

        textMesh.color = textColor;

        // Pede as estrelas para o PoolManager
        if (UIPoolManager.Instance != null)
        {
            for (int i = 0; i < finalStarAmount; i++)
            {
                UIPoolManager.Instance.SpawnMagicStar(transform.position);
            }
        }
    }

    private void Update()
    {
        transform.position += moveVector * Time.deltaTime;
        moveVector.y -= gravity * Time.deltaTime;

        lifetime += Time.deltaTime;
        float fractionOfJourney = lifetime / disappearTimerBase;
        float currentAnimatedScale = scaleCurve.Evaluate(fractionOfJourney);

        float distanceScaleMultiplier = 1f;
        if (useDistanceScaling && camTransform != null)
        {
            float distanceToCamera = Vector3.Distance(camTransform.position, transform.position);
            distanceScaleMultiplier = distanceToCamera / referenceDistance;
            distanceScaleMultiplier = Mathf.Clamp(distanceScaleMultiplier, 0.5f, maxScaleMultiplier);
        }

        transform.localScale = Vector3.one * (currentAnimatedScale * distanceScaleMultiplier);

        disappearTimer -= Time.deltaTime;
        if (disappearTimer < disappearTimerBase * 0.5f)
        {
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            if (textColor.a <= 0)
            {
                // DEVOLVE PARA O POOL
                if (UIPoolManager.Instance != null)
                {
                    UIPoolManager.Instance.ReturnPopupToPool(this);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (camTransform != null) transform.forward = camTransform.forward;
    }
}