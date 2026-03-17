using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class MagicStar : MonoBehaviour
{
    private ParticleSystem particleSys;
    private ParticleSystem.MainModule particleMain;
    private ParticleSystem.EmissionModule particleEmission;
    private ParticleSystemRenderer particleRenderer;

    private Vector3 currentVelocity;
    private float startTime;
    private bool isMoving = false;
    private Transform camTransform;

    [Header("Configurações do Movimento (Física)")]
    public float initialHopForce = 6f;
    public float gravityForce = 18f;
    public float scatterForce = 4f;
    public float duration = 1.0f;

    [Header("Ordem de Renderização")]
    public int sortingOrder = 2000;

    [Header("Configurações Visuais")]
    public AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1.2f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f)
    );

    public AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.1f, 1f),
        new Keyframe(0.8f, 1f),
        new Keyframe(1f, 0f)
    );

    private CanvasGroup canvasGroup;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        particleSys = GetComponent<ParticleSystem>();
        particleMain = particleSys.main;
        particleEmission = particleSys.emission;
        particleRenderer = particleSys.GetComponent<ParticleSystemRenderer>();

        canvasGroup = GetComponent<CanvasGroup>();
        TryGetComponent(out spriteRenderer);

        if (Camera.main != null) camTransform = Camera.main.transform;

        particleMain.loop = false;
        particleMain.playOnAwake = false;
        particleMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        particleMain.startSpeed = 0f;

        particleRenderer.alignment = ParticleSystemRenderSpace.Local;
        particleRenderer.sortingOrder = sortingOrder;

        particleEmission.enabled = true;
        particleEmission.rateOverTime = 0f;
        particleEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 1) });
    }

    void OnEnable()
    {
        // OBRIGATÓRIO PARA POOL: Limpa as partículas velhas ao renascer
        if (particleSys != null)
        {
            particleSys.Clear();
        }
    }

    public void StartParabolicMovement()
    {
        float randomX = Random.Range(-scatterForce, scatterForce);
        if (Mathf.Abs(randomX) < 1f) randomX = randomX >= 0 ? 1.5f : -1.5f;

        float randomY = Random.Range(initialHopForce * 0.8f, initialHopForce * 1.2f);
        currentVelocity = new Vector3(randomX, randomY, 0f);

        startTime = Time.time;
        isMoving = true;

        if (particleSys != null) particleSys.Play();
    }

    void Update()
    {
        if (!isMoving) return;

        float timeElapsed = Time.time - startTime;
        float fractionOfJourney = timeElapsed / duration;

        transform.position += currentVelocity * Time.deltaTime;
        currentVelocity.y -= gravityForce * Time.deltaTime;

        if (camTransform != null)
        {
            transform.rotation = camTransform.rotation;
            if (currentVelocity.sqrMagnitude > 0.1f)
            {
                float angle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg;
                transform.Rotate(0, 0, angle, Space.Self);
            }
        }

        transform.localScale = Vector3.one * scaleCurve.Evaluate(fractionOfJourney);

        float currentOpacity = alphaCurve.Evaluate(fractionOfJourney);
        if (canvasGroup != null) canvasGroup.alpha = currentOpacity;
        else if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = currentOpacity;
            spriteRenderer.color = c;
        }

        if (fractionOfJourney >= 1f)
        {
            isMoving = false;
            // DEVOLVE PARA O POOL em vez de Destruir
            if (UIPoolManager.Instance != null)
            {
                UIPoolManager.Instance.ReturnStarToPool(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}