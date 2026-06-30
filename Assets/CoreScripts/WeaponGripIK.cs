using UnityEngine;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(PlayerMovement))]
public class WeaponGripIK : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private Animator animator;

    [Header("Grip Rig Settings")]
    [Tooltip("Arraste o Rig da empunhadura (Grip Rig) aqui.")]
    public Rig gripRig;

    [Tooltip("Arraste o Aim Rig (da mira do corpo) aqui, para desativar a torção do corpo junto com a arma.")]
    public Rig aimRig;

    [Tooltip("A velocidade com que o peso do Rig muda (suavidade ao soltar a arma).")]
    public float lerpSpeed = 15f;

    [Tooltip("O peso final do Rig quando mirando (0 a 1).")]
    [Range(0, 1)] public float activeWeight = 1.0f;

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        animator = playerMovement.GetModelPivot().GetComponentInChildren<Animator>();
    }

    void Start()
    {
        // Garante que o Rig comece com peso 0
        if (gripRig != null) gripRig.weight = 0f;
    }

    void Update()
    {
        if (playerMovement != null && gripRig != null && animator != null)
        {
            // 1. Lê o aviso que criamos no Animator (se ela está atacando, recarregando, etc)
            bool isIKDisabled = animator.GetBool("DisableIK");

            // 2. Calcula o peso alvo: Se ela está mirando e o IK NÃO está desabilitado, peso = 1. Senão, peso = 0.
            float targetWeight = (playerMovement.isAiming && !isIKDisabled) ? activeWeight : 0f;

            // 3. Aplica a transição suave para a mão esquerda soltar/pegar a arma
            gripRig.weight = Mathf.Lerp(gripRig.weight, targetWeight, lerpSpeed * Time.deltaTime);

            // 4. BÔNUS: Se você associou o AimRig no Inspector, ele também para de mirar o corpo 
            // durante o ataque da katana ou reload, evitando que o tronco quebre.
            if (aimRig != null && isIKDisabled)
            {
                aimRig.weight = Mathf.Lerp(aimRig.weight, 0f, lerpSpeed * Time.deltaTime);
            }
        }
    }
}