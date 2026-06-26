using UnityEngine;

/// <summary>
/// Script simples para fazer um VFX voar em linha reta na direção que ele nasce olhando.
/// Basta anexar ao Prefab da flecha (ArrowUlt). Ao ser instanciado, ele automaticamente
/// voa para frente (transform.forward) na velocidade configurada e se destrói após o tempo limite.
/// </summary>
public class FlyForward : MonoBehaviour
{
    [Header("Configurações de Voo")]
    [Tooltip("Velocidade em metros por segundo.")]
    public float speed = 80f;

    [Tooltip("Tempo em segundos até o efeito ser destruído.")]
    public float lifetime = 3f;

    private Vector3 flyDirection;
    private float timer = 0f;

    void Start()
    {
        // Captura a direção no momento em que o objeto é criado e nunca mais muda,
        // garantindo que a flecha voe em linha reta mesmo se algo rotacionar o objeto.
        flyDirection = transform.forward;
    }

    void Update()
    {
        transform.position += flyDirection * speed * Time.deltaTime;

        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
