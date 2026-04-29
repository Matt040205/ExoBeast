using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX; // Necessário para acessar o VisualEffect

public class GroundSlash : MonoBehaviour
{
    [Header("Configurações de Movimento")]
    public float speed = 14f;
    public float slowDownRate = 0.5f;
    public float detectingDistance = 3f;

    [Tooltip("Tempo que a onda viaja antes de começar a sumir")]
    public float travelTime = 1.5f;

    [Tooltip("Tempo extra para as partículas terminarem o fade out antes do Destroy")]
    public float fadeOutGracePeriod = 2.0f;

    private Rigidbody rb;
    private VisualEffect vfx;
    private bool stopped = false;

    void Start()
    {
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
        rb = GetComponent<Rigidbody>();
        vfx = GetComponent<VisualEffect>();

        StartCoroutine(EffectRoutine());
    }

    IEnumerator EffectRoutine()
    {
        // 1. Viaja durante o tempo definido
        yield return new WaitForSeconds(travelTime);

        // 2. Começa a frear
        float t = 1;
        while (t > 0)
        {
            rb.linearVelocity = Vector3.Lerp(Vector3.zero, rb.linearVelocity, t);
            t -= slowDownRate * Time.deltaTime;
            yield return null;
        }
        stopped = true;

        // 3. Manda o VFX parar de spawnar (isso permite que as partículas vivas terminem o lifetime)
        if (vfx != null)
        {
            vfx.Stop();
        }

        // 4. Espera o tempo das últimas partículas sumirem suavemente
        yield return new WaitForSeconds(fadeOutGracePeriod);

        // 5. Agora sim, destrói o objeto
        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (!stopped)
        {
            RaycastHit hit;
            Vector3 rayStart = transform.position + new Vector3(0, 1, 0);

            if (Physics.Raycast(rayStart, Vector3.down, out hit, detectingDistance))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            }
        }
    }
}