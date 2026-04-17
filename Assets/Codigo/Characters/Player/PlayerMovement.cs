using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using System.Collections;
using FMODUnity;
using FMOD.Studio;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// ── PlayerMovement ─────────────────────────────────────
/// Movimentacao do jogador via CharacterController com gravidade e pulo.
///
///  ▸ Owner: processa input (WASD, pulo, aim), move CharacterController
///  ▸ Remoto: desativa CharacterController (ClientNetworkTransform sincroniza posicao)
///  ▸ Usa NetworkAnimator para sincronizar triggers (Jump)
///  ▸ Integra com FMOD para sons de passos
///  ▸ Suporta pulo duplo, dash externo e flutuacao (habilidades)
/// ─────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float jumpForce = 4f;
    public float gravity = -9.81f;
    public float rotationSpeed = 15f;

    [Header("References")]
    public Transform cameraController;
    public Transform modelPivot;

    [Header("Aiming Settings")]
    public Rig aimRig;
    public MultiAimConstraint aimConstraint;
    public LayerMask aimLayerMask;
    public Transform aimTarget;

    [Header("Ground Check & Landing")]
    public LayerMask groundMask;
    public float landingRaycastDistance = 1.2f;
    private bool isAboutToLand;

    [Header("FMOD")]
    [EventRef] public string eventoPassos = "event:/SFX/Passos";
    private EventInstance passosSoundInstance;
    private bool isPlayingFootsteps = false;

    [HideInInspector] public bool isDashing = false;

    public bool isAiming = false;

    private CharacterController controller;
    private Vector3 velocity;
    public bool isGrounded;
    private float currentSpeed;
    private float rotationVelocity;

    private Animator animator;
    private NetworkAnimator networkAnimator; // <--- CACHE SEGURO ADICIONADO AQUI
    private Vector3 direction;
    private float targetAngle;

    public bool canDoubleJump = false;
    private bool hasDoubleJumped = false;
    public bool isFloating = false;
    public float floatDuration = 0f;
    public float jumpHeightModifier = 1f;

    [Header("Network Sync")]
    private NetworkVariable<float> netModelYRot = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> netMovementSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> netYVelocity = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private bool jaMoveuTutorial = false;
    private PlayerHealthSystem healthSystem;

    private Vector2 inputMove;
    private bool inputRun;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        healthSystem = GetComponent<PlayerHealthSystem>();

        if (modelPivot != null)
            animator = modelPivot.GetComponentInChildren<Animator>();

        // Tenta achar o NetworkAnimator na raiz, se não achar, procura nos filhos (onde o modelo 3D geralmente fica)
        networkAnimator = GetComponent<NetworkAnimator>();
        if (networkAnimator == null) networkAnimator = GetComponentInChildren<NetworkAnimator>();

        if (!string.IsNullOrEmpty(eventoPassos))
        {
            passosSoundInstance = RuntimeManager.CreateInstance(eventoPassos);
            RuntimeManager.AttachInstanceToGameObject(passosSoundInstance, transform);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            // CharacterController desabilitado: ClientNetworkTransform controla a posicao
            if (controller != null) controller.enabled = false;

            // Rig de mira nao interfere nos remotos
            if (aimRig != null) aimRig.weight = 0f;

            // Script permanece ATIVO para que o Update() possa aplicar animacoes sincronizadas
            return;
        }

        InitializeOwner();
    }

    private void InitializeOwner()
    {
        if (cameraController == null && Camera.main != null)
        {
            cameraController = Camera.main.transform;
        }

        if (aimRig != null) aimRig.weight = 0f;

        if (TutorialManager.Instance != null && GameDataManager.Instance != null)
        {
            if (GameDataManager.Instance.tutoriaisConcluidos.Contains("PLAYER_MOVEMENT"))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                // PLAYER_MOVEMENT -> quando fechar -> EXPLAIN_BUILD_MODE
                TutorialManager.Instance.TriggerTutorial("PLAYER_MOVEMENT", () =>
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    if (TutorialManager.Instance != null)
                        TutorialManager.Instance.TriggerTutorial("EXPLAIN_BUILD_MODE");
                });
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Vincular ao TopDownCameraManager se disponível
        if (TopDownCameraManager.Instance != null)
            TopDownCameraManager.Instance.SetCameraTarget(transform);
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (!IsOwner) return;
        inputMove = ctx.ReadValue<Vector2>();
    }

    public void OnRun(InputAction.CallbackContext ctx)
    {
        if (!IsOwner) return;
        inputRun = ctx.ReadValueAsButton();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!IsOwner) return;
        if (!ctx.started) return;
        if (GetComponent<MergulhoTintaLogic>() != null) return;
        if (PauseControl.isPaused || BuildManager.isBuildingMode || isFloating || isDashing) return;

        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity) * jumpHeightModifier;
            isGrounded = false;

            if (animator != null)
            {
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("Shoot");
                animator.ResetTrigger("Reload");
            }

            // CORREÇÃO DA LINHA 172: Usando a referência segura para o NetworkAnimator
            if (networkAnimator != null) networkAnimator.SetTrigger("Jump");

            StopFootstepSound();
        }
        else if (canDoubleJump && !hasDoubleJumped)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity) * jumpHeightModifier;
            hasDoubleJumped = true;

            if (animator != null)
            {
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("Shoot");
                animator.ResetTrigger("Reload");
            }

            // CORREÇÃO DO PULO DUPLO TAMBÉM:
            if (networkAnimator != null) networkAnimator.SetTrigger("Jump");

            StopFootstepSound();
        }
    }

    public void OnAim(InputAction.CallbackContext ctx)
    {
        if (!IsOwner) return;
        bool aimingInput = ctx.ReadValueAsButton();

        if (aimingInput != isAiming)
        {
            isAiming = aimingInput;

            if (animator != null)
            {
                animator.SetBool("isAiming", isAiming);
            }

            StopAllCoroutines();
            StartCoroutine(FadeRigWeight(isAiming ? 1f : 0f));
        }
    }

    private void Update()
    {
        if (!IsOwner)
        {
            // Remotos: aplicar estado sincronizado ao Animator
            if (animator != null)
            {
                bool syncedGrounded = netIsGrounded.Value;
                float syncedYVel = netYVelocity.Value;

                animator.SetBool("isGrounded", syncedGrounded);
                animator.SetFloat("yVelocity", syncedYVel);
                animator.SetFloat("MovementSpeed", netMovementSpeed.Value);

                bool aboutToLand = !syncedGrounded && syncedYVel < 0 &&
                    Physics.Raycast(transform.position, Vector3.down, landingRaycastDistance, groundMask);
                animator.SetBool("isAboutToLand", syncedGrounded || aboutToLand);
            }
            return;
        }

        isGrounded = controller.isGrounded;
        if (isGrounded) hasDoubleJumped = false;

        if (PauseControl.isPaused || BuildManager.isBuildingMode || isDashing)
        {
            if (animator != null && !isDashing) animator.SetFloat("MovementSpeed", 0f);
            netMovementSpeed.Value = 0f;
            StopFootstepSound();
            return;
        }

        if (isFloating)
        {
            velocity.y = 0;
            floatDuration -= Time.deltaTime;
            if (floatDuration <= 0) isFloating = false;
            StopFootstepSound();
        }
        else
        {
            HandleMovement();
            ApplyGravity();
        }

        if (aimTarget != null && cameraController != null)
        {
            Ray ray = new Ray(cameraController.position, cameraController.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 999f, aimLayerMask))
                aimTarget.position = hit.point;
            else
                aimTarget.position = ray.GetPoint(100f);
        }

        if (animator != null)
        {
            animator.SetBool("isGrounded", isGrounded);
            animator.SetFloat("yVelocity", velocity.y);

            if (!isGrounded && velocity.y < 0)
            {
                isAboutToLand = Physics.Raycast(transform.position, Vector3.down, landingRaycastDistance, groundMask);
            }
            else
            {
                isAboutToLand = false;
            }

            if (isGrounded)
            {
                isAboutToLand = true;
            }

            animator.SetBool("isAboutToLand", isAboutToLand);
        }

        // Publicar estado para remotos
        netIsGrounded.Value = isGrounded;
        netYVelocity.Value = velocity.y;
    }

    private void LateUpdate()
    {
        if (!IsOwner)
        {
            // Remotos: aplicar rotacao sincronizada ao modelPivot
            if (modelPivot != null)
                modelPivot.rotation = Quaternion.Euler(0f, netModelYRot.Value, 0f);
            return;
        }

        if (PauseControl.isPaused || BuildManager.isBuildingMode || isFloating || isDashing) return;

        if (isAiming || direction.sqrMagnitude > 0.01f)
        {
            if (isAiming && cameraController != null)
            {
                targetAngle = cameraController.eulerAngles.y;
            }
            float angle = Mathf.SmoothDampAngle(modelPivot.eulerAngles.y, targetAngle, ref rotationVelocity, 0.1f);
            modelPivot.rotation = Quaternion.Euler(0f, angle, 0f);

            // Publicar rotacao para remotos
            netModelYRot.Value = angle;
        }
    }

    private void HandleMovement()
    {
        direction = new Vector3(inputMove.x, 0f, inputMove.y);
        currentSpeed = inputRun ? runSpeed : walkSpeed;

        float finalSpeed = currentSpeed;
        if (healthSystem != null) finalSpeed *= healthSystem.speedMultiplier.Value;

        if (direction.sqrMagnitude > 0.01f)
        {
            // Tutorial de build mode agora e encadeado via callback no PLAYER_MOVEMENT
            // Nao precisa mais do trigger aqui

            Vector3 moveDir;

            if (isAiming)
            {
                float lookAngle = cameraController.eulerAngles.y;
                moveDir = Quaternion.Euler(0f, lookAngle, 0f) * direction;

                if (animator != null)
                {
                    animator.SetFloat("AimMoveX", inputMove.x, 0.1f, Time.deltaTime);
                    animator.SetFloat("AimMoveY", inputMove.y, 0.1f, Time.deltaTime);
                }
            }
            else
            {
                targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraController.eulerAngles.y;
                moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

                if (animator != null)
                {
                    float animSpeed = (inputRun ? 1.0f : 0.5f) * direction.magnitude;
                    if (healthSystem != null && healthSystem.speedMultiplier.Value > 1.1f) animSpeed *= 1.2f;
                    animator.SetFloat("MovementSpeed", animSpeed, 0.1f, Time.deltaTime);
                    netMovementSpeed.Value = animSpeed;
                }
            }

            controller.Move(moveDir.normalized * finalSpeed * Time.deltaTime);
            if (isGrounded) PlayFootstepSound();
            else StopFootstepSound();
        }
        else
        {
            controller.Move(Vector3.zero);
            if (animator != null)
            {
                animator.SetFloat("MovementSpeed", 0f, 0.1f, Time.deltaTime);
                animator.SetFloat("AimMoveX", 0f, 0.1f, Time.deltaTime);
                animator.SetFloat("AimMoveY", 0f, 0.1f, Time.deltaTime);
            }
            netMovementSpeed.Value = 0f;
            StopFootstepSound();
        }
    }

    private void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;

            if (animator != null)
            {
                animator.ResetTrigger("Jump");
            }
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private IEnumerator FadeRigWeight(float targetWeight)
    {
        if (aimRig == null) yield break;
        float time = 0f;
        float startWeight = aimRig.weight;
        float duration = 0.2f;
        while (time < duration)
        {
            aimRig.weight = Mathf.Lerp(startWeight, targetWeight, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        aimRig.weight = targetWeight;
    }

    private void PlayFootstepSound()
    {
        if (!isPlayingFootsteps && passosSoundInstance.isValid())
        {
            passosSoundInstance.start();
            isPlayingFootsteps = true;
        }
    }

    private void StopFootstepSound()
    {
        if (isPlayingFootsteps && passosSoundInstance.isValid())
        {
            passosSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            isPlayingFootsteps = false;
        }
    }

    private void OnDestroy()
    {
        if (passosSoundInstance.isValid()) passosSoundInstance.release();
    }

    public Transform GetModelPivot() => modelPivot;
}