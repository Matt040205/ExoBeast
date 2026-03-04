using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static Unity.VisualScripting.Member;

public class CacadoraNoturnaLogic : MonoBehaviour
{
    public ParticleSystem effectParticles;
    public GameObject beamVisualPrefab;
    public float visualDuration = 0.5f;

    private float damage;
    private float range;
    private float width;
    private GameObject caster;
    private LayerMask visualRaycastMask;

    private Animator anim;

    public void StartUltimateEffect(GameObject caster, float damage, float range, float width)
    {
        this.caster = caster;
        this.damage = damage;
        this.range = range;
        this.width = width;

        LayerMask enemyLayer = LayerMask.GetMask("Enemy");
        LayerMask playerLayer = LayerMask.GetMask("Player");
        visualRaycastMask = ~(enemyLayer | playerLayer);

        if (caster != null)
        {
            this.anim = caster.GetComponentInChildren<Animator>();

            // !! A CONEXÃO PERFEITA !!
            // Assim que a magia nasce, ela procura o Proxy no personagem e avisa: 
            // "Ei, eu sou a magia atual. Quando a animação pedir, dispare A MIM!"
            AnimationEventProxy proxy = caster.GetComponentInChildren<AnimationEventProxy>();
            if (proxy != null)
            {
                proxy.magiaAtualDaCacadora = this;
            }
        }

        if (effectParticles != null)
        {
            effectParticles.Play();
        }

        if (this.anim != null)
        {
            this.anim.SetTrigger("CacadoraUltimate");
        }

        Destroy(gameObject, visualDuration + 3.0f);
    }

    public void AnimEvent_FireBeam()
    {
        Debug.Log("[CAÇADORA] Evento de Animação recebido! Disparando raio na sincronia perfeita!");

        ApplyBeamDamage();

        if (beamVisualPrefab != null)
        {
            StartCoroutine(ShowBeamVisual());
        }
        else
        {
            Debug.LogWarning("[CAÇADORA] O visual não apareceu porque o 'beamVisualPrefab' está vazio no Inspector da Magia!");
        }
    }

    private IEnumerator ShowBeamVisual()
    {
        Vector3 startPoint = transform.position;
        Vector3 direction = transform.forward;
        float beamDistance = range;

        RaycastHit groundHit;
        if (Physics.Raycast(startPoint, direction, out groundHit, range, visualRaycastMask))
        {
            beamDistance = groundHit.distance;
        }

        GameObject visual = Instantiate(beamVisualPrefab, startPoint, transform.rotation);
        visual.transform.SetParent(this.transform);

        LineRenderer line = visual.GetComponent<LineRenderer>();
        if (line != null)
        {
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * beamDistance);
            line.startWidth = width;
            line.endWidth = width;
        }

        float elapsedTime = 0f;
        Material lineMaterial = line?.material;
        Color originalColor = Color.white;
        if (lineMaterial != null && lineMaterial.HasColor("_Color"))
        {
            originalColor = lineMaterial.color;
        }

        while (elapsedTime < visualDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / visualDuration;

            if (lineMaterial != null)
            {
                originalColor.a = 1f - progress;
                lineMaterial.color = originalColor;
            }
            yield return null;
        }

        Destroy(visual);
    }

    private void ApplyBeamDamage()
    {
        LayerMask enemyLayer = LayerMask.GetMask("Enemy");
        Vector3 startPoint = transform.position;
        Vector3 direction = transform.forward;

        RaycastHit[] inimigosAcertados = Physics.SphereCastAll(startPoint, width, direction, range, enemyLayer);
        Debug.Log($"[HabilidadeCacadoraNoturna] Raio disparado. Acertou {inimigosAcertados.Length} inimigos.");

        foreach (var hit in inimigosAcertados)
        {
            EnemyHealthSystem vidaInimigo = hit.collider.GetComponent<EnemyHealthSystem>();
            if (vidaInimigo != null)
            {
                vidaInimigo.TakeDamage(damage, 0f, false);
            }
        }
    }
}