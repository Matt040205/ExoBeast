using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using System.Collections;
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

    [Header("VFX Especiais")]
    [Tooltip("Prefab do efeito de vento para pulos múltiplos (ex: Voo Gracioso da Coruja)")]
    public GameObject doubleJumpVfxPrefab;

    [Header("Aiming Settings")]
    public Rig aimRig;
    public MultiAimConstraint aimConstraint;
    public LayerMask aimLayerMask;
    public Transform aimTarget;

    [Header("Ground Check & Landing")]
    public LayerMask groundMask;
    public float landingRaycastDistance = 1.2f;
    [Header("FMOD - Passos por Superfície")]
    public string eventoPassosTerra = AudioEventIds.PlayerFootstepsDirt;
    public string eventoPassosConcreto = AudioEventIds.PlayerFootstepsConcrete;
    public string eventoPassosAgua = AudioEventIds.PlayerFootstepsWater;
    private AudioLoopHandle passosLoop;
    private bool isPlayingFootsteps = false;
    private TerrainSurfaceDetector surfaceDetector;
    private TerrainSurfaceDetector.SurfaceType lastSurface = TerrainSurfaceDetector.SurfaceType.Terra;

    [HideInInspector] public bool isDashing = false;

    public bool isAiming = false;

    private CharacterController controller;
    private Vector3 velocity;
    public bool isGrounded;
    private float currentSpeed;
    private float rotationVelocity;

    private UniversalCharacterAnimator universalAnimator;
    private Vector3 direction;
    private float targetAngle;

    public bool canDoubleJump = false;
    private bool hasDoubleJumped = false;
    public bool isFloating = false;
    public float floatDuration = 0f;
    public float jumpHeightModifier = 1f;

    private struct PlayerAnimState : INetworkSerializable, System.IEquatable<PlayerAnimState>
    {
        // Mantem margem para o buff existente que publica 1.2f no Animator remoto.
        private const float MaxMovementAnimSpeed = 1.25f;
        private const float MaxAbsYVelocity = 10f;

        public byte movementSpeed;
        public byte yVelocity;

        public float MovementSpeed => (movementSpeed / 255f) * MaxMovementAnimSpeed;
        public float YVelocity => ((yVelocity - 128) / 127f) * MaxAbsYVelocity;

        public static PlayerAnimState FromValues(float movementSpeedValue, float yVelocityValue)
        {
            float clampedMovement = Mathf.Clamp(movementSpeedValue, 0f, MaxMovementAnimSpeed);
            float clampedY = Mathf.Clamp(yVelocityValue, -MaxAbsYVelocity, MaxAbsYVelocity);

            return new PlayerAnimState
            {
                movementSpeed = (byte)Mathf.RoundToInt((clampedMovement / MaxMovementAnimSpeed) * 255f),
                yVelocity = (byte)(Mathf.RoundToInt((clampedY / MaxAbsYVelocity) * 127f) + 128)
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref movementSpeed);
            serializer.SerializeValue(ref yVelocity);
        }

        public bool Equals(PlayerAnimState other)
        {
            return movementSpeed == other.movementSpeed && yVelocity == other.yVelocity;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerAnimState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (movementSpeed << 8) | yVelocity;
        }
    }

    [Header("Network Sync")]
    private NetworkVariable<float> netModelYRot = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<PlayerAnimState> netAnimState = new NetworkVariable<PlayerAnimState>(
        PlayerAnimState.FromValues(0f, 0f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private PlayerHealthSystem healthSystem;

    [HideInInspector]
    public NetworkVariable<bool> netIsWebTrapped = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private LocalPlayerInputBridge inputBridge;
    private bool loggedMovementFallbackWithoutCamera;
    private float currentAnimatorMovementSpeed;

    private Vector2 inputMove;
    private bool inputRun;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        healthSystem = GetComponent<PlayerHealthSystem>();
        inputBridge = GetComponent<LocalPlayerInputBridge>();
        TryResolveCriticalReferences(false);

        universalAnimator = GetComponent<UniversalCharacterAnimator>();
        if (universalAnimator == null) universalAnimator = GetComponentInChildren<UniversalCharacterAnimator>();
        if (universalAnimator == null)
        {
            universalAnimator = gameObject.AddComponent<UniversalCharacterAnimator>();
        }

        surfaceDetector = GetComponent<TerrainSurfaceDetector>();
        CreateFootstepInstance(GetEventForSurface(TerrainSurfaceDetector.SurfaceType.Terra));
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        TryResolveCriticalReferences(true);

        if (!IsOwner)
        {
            // CharacterController desabilitado: ClientNetworkTransform controla a posicao
            if (controller != null) controller.enabled = false;

            // Rig de mira nao interfere nos remotos
            if (aimRig != null) aimRig.weight = 0f;

            // Quando PlayerNetworkSetup nao esta no prefab (clientes NGO nao recebem
            // componentes adicionados em runtime pelo servidor via EnsureRuntimePlayerNetworkContract),
            // desabilitar PlayerInput aqui para evitar conflito de keyboard device pairing.
            if (GetComponent<ExoBeasts.Multiplayer.Sync.PlayerNetworkSetup>() == null)
            {
                var pi = GetComponent<PlayerInput>();
                if (pi != null) pi.enabled = false;
            }

            // Script permanece ATIVO para que o Update() possa aplicar animacoes sincronizadas
            return;
        }

        InitializeOwner();

        // Fallback: se PlayerNetworkSetup nao foi injetado pelo servidor (cliente NGO),
        // inicializar o PlayerInput e LocalPlayerInputBridge aqui.
        if (GetComponent<ExoBeasts.Multiplayer.Sync.PlayerNetworkSetup>() == null)
            StartCoroutine(SetupOwnerInputFallback());
    }

    private void InitializeOwner()
    {
        if (controller != null && !controller.enabled)
            controller.enabled = true;

        if (inputBridge == null)
            inputBridge = GetComponent<LocalPlayerInputBridge>();

        TryResolveCriticalReferences(false);

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
        if (!IsOwner || UsesPolledInput()) return;
        inputMove = ctx.ReadValue<Vector2>();
    }

    public void OnRun(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || UsesPolledInput()) return;
        inputRun = ctx.ReadValueAsButton();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || UsesPolledInput()) return;
        if (!ctx.started) return;
        HandleJumpPressed();
    }

    private void HandleJumpPressed()
    {
        if (netIsWebTrapped.Value)
        {
            RequestBreakWebSpacePressServerRpc();
            return;
        }
        if (GetComponent<MergulhoTintaLogic>() != null) return;
        if (PauseControl.isPaused || BuildManager.isBuildingMode || isFloating || isDashing) return;

        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity) * jumpHeightModifier;
            isGrounded = false;

            if (universalAnimator != null)
            {
                universalAnimator.ResetActionTrigger();
                universalAnimator.TriggerAction(CharacterActionID.Jump);
            }

            StopFootstepSound();
        }
        else if (canDoubleJump && !hasDoubleJumped)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity) * jumpHeightModifier;
            hasDoubleJumped = true;

            if (universalAnimator != null)
            {
                universalAnimator.ResetActionTrigger();
                universalAnimator.TriggerAction(CharacterActionID.Jump);
            }

            // Instancia o VFX localmente (Zero Latency) e avisa a rede
            if (doubleJumpVfxPrefab != null)
            {
                GlobalVFXPool.GetVFX(doubleJumpVfxPrefab, transform.position, transform.rotation, 2f);
            }
            RequestDoubleJumpVfxServerRpc();

            StopFootstepSound();
        }
    }

    public void OnAim(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || UsesPolledInput()) return;
        SetAimState(ctx.ReadValueAsButton());
    }

    private void SetAimState(bool aimingInput)
    {
        if (aimingInput != isAiming)
        {
            isAiming = aimingInput;

            if (universalAnimator != null)
            {
                universalAnimator.SetAiming(isAiming);
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
            if (universalAnimator != null)
            {
                bool localGrounded = ProbeGrounded(0.35f);
                PlayerAnimState syncedAnimState = netAnimState.Value;
                float syncedYVel = syncedAnimState.YVelocity;

                universalAnimator.UpdateAirborne(localGrounded, syncedYVel);
                universalAnimator.UpdateMovement(syncedAnimState.MovementSpeed, dampTime: 0f);
            }
            return;
        }

        SyncOwnerInputFromBridge();

        isGrounded = controller.isGrounded;
        if (isGrounded) hasDoubleJumped = false;

        if (PauseControl.isPaused || isDashing)
        {
            if (universalAnimator != null && !isDashing) universalAnimator.UpdateMovement(0f, dampTime: 0f);
            currentAnimatorMovementSpeed = 0f;
            SetNetworkAnimState(0f, velocity.y);
            StopFootstepSound();
            return;
        }

        if (BuildManager.isBuildingMode)
        {
            if (universalAnimator != null) universalAnimator.UpdateMovement(0f, dampTime: 0f);
            currentAnimatorMovementSpeed = 0f;
            SetNetworkAnimState(0f, velocity.y);
            StopFootstepSound();
            ApplyGravity();
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

        if (universalAnimator != null)
        {
            universalAnimator.UpdateAirborne(isGrounded, velocity.y);
        }

        // Publicar estado para remotos
        SetNetworkAnimState(currentAnimatorMovementSpeed, velocity.y);
    }

    private void LateUpdate()
    {
        if (modelPivot == null)
        {
            TryResolveCriticalReferences(IsOwner);
            if (modelPivot == null) return;
        }

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
        TryResolveCriticalReferences(false);

        if (netIsWebTrapped.Value)
        {
            controller.Move(Vector3.zero);
            if (universalAnimator != null)
            {
                universalAnimator.UpdateMovement(0f, dampTime: 0.1f);
            }
            currentAnimatorMovementSpeed = 0f;
            StopFootstepSound();
            return;
        }

        direction = new Vector3(inputMove.x, 0f, inputMove.y);
        currentSpeed = inputRun ? runSpeed : walkSpeed;

        float finalSpeed = currentSpeed;
        if (healthSystem != null) finalSpeed *= healthSystem.speedMultiplier.Value;
        finalSpeed = ModificacaoRunState.ApplyPlayerMoveSpeedMultiplier(finalSpeed);

        if (direction.sqrMagnitude > 0.01f)
        {
            bool hasCameraBasis = TryGetMovementBasis(out Vector3 basisForward, out Vector3 basisRight);
            Vector3 moveDir = (basisRight * direction.x) + (basisForward * direction.z);

            if (moveDir.sqrMagnitude > 0.0001f)
                moveDir.Normalize();

            if (isAiming)
            {
                targetAngle = Mathf.Atan2(basisForward.x, basisForward.z) * Mathf.Rad2Deg;

                if (universalAnimator != null)
                {
                    universalAnimator.UpdateMovement(0f, inputMove.x, inputMove.y, dampTime: 0.1f);
                }
            }
            else
            {
                targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;

                if (universalAnimator != null)
                {
                    float animSpeed = (inputRun ? 1.0f : 0.5f) * direction.magnitude;
                    if (healthSystem != null && healthSystem.speedMultiplier.Value > 1.1f) animSpeed *= 1.2f;
                    universalAnimator.UpdateMovement(animSpeed, dampTime: 0.1f);
                    currentAnimatorMovementSpeed = animSpeed;
                }
            }

            if (!hasCameraBasis && !loggedMovementFallbackWithoutCamera)
            {
                Debug.LogWarning("[PlayerMovement] cameraController ainda nao estava pronto; usando orientacao fallback para nao bloquear o movimento do owner.");
                loggedMovementFallbackWithoutCamera = true;
            }
            else if (hasCameraBasis)
            {
                loggedMovementFallbackWithoutCamera = false;
            }

            controller.Move(moveDir * finalSpeed * Time.deltaTime);
            if (isGrounded) PlayFootstepSound();
            else StopFootstepSound();
        }
        else
        {
            controller.Move(Vector3.zero);
            if (universalAnimator != null)
            {
                universalAnimator.UpdateMovement(0f, 0f, 0f, dampTime: 0.1f);
            }
            currentAnimatorMovementSpeed = 0f;
            StopFootstepSound();
        }
    }

    /// <summary>
    /// BUG FIX (Bug 9 - 7 Maio 2026): forca o modelPivot a apontar imediatamente para a direcao
    /// horizontal da camera. Usado pelo MeleeCombatSystem para garantir que o ataque sai na
    /// direcao certa mesmo quando o personagem esta parado e sem aim ativo (caso da Dragao).
    /// Sem isso, LateUpdate so rotaciona se isAiming || direction.sqrMagnitude > 0.01f, entao
    /// o melee ficava preso na rotacao do spawn.
    /// </summary>
    public void FaceCameraImmediately()
    {
        if (!IsOwner) return;
        if (modelPivot == null || cameraController == null) return;

        float cameraYaw = cameraController.eulerAngles.y;
        modelPivot.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
        targetAngle = cameraYaw;        // alinha o targetAngle do SmoothDamp para nao "puxar" de volta
        netModelYRot.Value = cameraYaw; // replica para remotos
    }

    public bool IsGroundedNetworkState()
    {
        if (IsSpawned && !IsOwner)
            return ProbeGrounded(0.35f);

        if (controller != null && controller.enabled)
            return controller.isGrounded || isGrounded;

        return isGrounded;
    }

    public bool IsAirborneForAbility()
    {
        return !IsGroundedNetworkState();
    }

    public void BeginFloating(float duration, float newJumpHeightModifier)
    {
        if (duration <= 0f)
            return;

        if (newJumpHeightModifier > 0f)
            jumpHeightModifier = newJumpHeightModifier;

        velocity.y = 0f;
        isGrounded = false;
        isFloating = true;
        floatDuration = Mathf.Max(floatDuration, duration);
        StopFootstepSound();

        if (IsSpawned && IsOwner)
        {
            SetNetworkAnimState(currentAnimatorMovementSpeed, 0f);
        }
    }

    public void EndFloating(bool resetJumpModifier = true)
    {
        isFloating = false;
        floatDuration = 0f;

        if (resetJumpModifier)
            jumpHeightModifier = 1f;
    }

    private void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;

            if (universalAnimator != null)
            {
                universalAnimator.ResetActionTrigger();
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
        // Detecta mudança de superfície e troca o evento se necessário
        if (surfaceDetector != null)
        {
            TerrainSurfaceDetector.SurfaceType current = surfaceDetector.CurrentSurface;
            if (current != lastSurface)
            {
                lastSurface = current;
                string newEvent = GetEventForSurface(current);
                if (!string.IsNullOrEmpty(newEvent))
                {
                    bool wasPlaying = isPlayingFootsteps;
                    StopFootstepSound();
                    ReleaseFootstepInstance();
                    CreateFootstepInstance(newEvent);
                    if (wasPlaying) { ExoAudioService.StartLoop(ref passosLoop); isPlayingFootsteps = passosLoop.IsValid; }
                }
            }
        }

        if (!isPlayingFootsteps && passosLoop.IsValid)
        {
            ExoAudioService.StartLoop(ref passosLoop);
            isPlayingFootsteps = true;
        }
    }

    private void StopFootstepSound()
    {
        if (isPlayingFootsteps && passosLoop.IsValid)
        {
            ExoAudioService.StopLoop(ref passosLoop, allowFadeout: true, release: false);
            isPlayingFootsteps = false;
        }
    }

    private string GetEventForSurface(TerrainSurfaceDetector.SurfaceType surface)
    {
        switch (surface)
        {
            case TerrainSurfaceDetector.SurfaceType.Concreto: return eventoPassosConcreto;
            case TerrainSurfaceDetector.SurfaceType.Agua:     return eventoPassosAgua;
            case TerrainSurfaceDetector.SurfaceType.Terra:
            default:                                          return eventoPassosTerra;
        }
    }

    private void CreateFootstepInstance(string eventPath)
    {
        if (!string.IsNullOrEmpty(eventPath))
        {
            passosLoop = ExoAudioService.CreateLoop(eventPath, transform);

        }
    }

    private void ReleaseFootstepInstance()
    {
        ExoAudioService.ReleaseLoop(ref passosLoop);
    }

    public override void OnDestroy()
    {
        ReleaseFootstepInstance();
        base.OnDestroy();
    }

    [ServerRpc]
    private void RequestDoubleJumpVfxServerRpc()
    {
        PlayDoubleJumpVfxClientRpc();
    }

    [ClientRpc]
    private void PlayDoubleJumpVfxClientRpc()
    {
        if (IsOwner) return; // O dono já instanciou localmente

        if (doubleJumpVfxPrefab != null)
        {
            GlobalVFXPool.GetVFX(doubleJumpVfxPrefab, transform.position, transform.rotation, 2f);
        }
    }

    public void ResetMotionAfterTeleport()
    {
        velocity = Vector3.zero;
        direction = Vector3.zero;
        currentSpeed = 0f;
        rotationVelocity = 0f;
        isDashing = false;
        isFloating = false;
        floatDuration = 0f;
        hasDoubleJumped = false;
        isGrounded = true;

        StopFootstepSound();

        if (modelPivot != null)
        {
            modelPivot.localPosition = Vector3.zero;
        }

        if (universalAnimator != null)
        {
            universalAnimator.Rebind(); // Força o Animator a recalcular posição (corrige o bug do Rig ficar no lugar)
            universalAnimator.ResetActionTrigger();
            universalAnimator.UpdateAirborne(true, 0f);
            universalAnimator.UpdateMovement(0f, 0f, 0f, dampTime: 0f);
        }

        if (IsSpawned && IsOwner)
        {
            currentAnimatorMovementSpeed = 0f;
            SetNetworkAnimState(0f, 0f);
        }
    }

    private void SetNetworkAnimState(float movementSpeedValue, float yVelocityValue)
    {
        if (!IsSpawned || !IsOwner)
            return;

        // OPTIMIZATION (Sprint 3 / Item G3.3 - 2026-05-08): compacta MovementSpeed
        // e yVelocity em um unico NetworkVariable de 2 bytes, preservando o modelo
        // atual de animacao remota sem derivar velocidade por delta de posicao.
        PlayerAnimState nextState = PlayerAnimState.FromValues(movementSpeedValue, yVelocityValue);
        if (!netAnimState.Value.Equals(nextState))
            netAnimState.Value = nextState;
    }

    public Transform GetModelPivot()
    {
        if (modelPivot == null)
            TryResolveCriticalReferences(false);

        return modelPivot;
    }

    private void TryResolveCriticalReferences(bool logErrors)
    {
        if (cameraController == null)
        {
            CameraController localCameraController = GetComponent<CameraController>();
            if (localCameraController == null)
                localCameraController = GetComponentInChildren<CameraController>(true);

            if (localCameraController != null)
                cameraController = localCameraController.transform;
            else if (Camera.main != null)
                cameraController = Camera.main.transform;
        }

        if (modelPivot == null)
        {
            Transform namedPivot = transform.Find("ModelPivot");
            if (namedPivot != null)
            {
                modelPivot = namedPivot;
            }
            else
            {
                Animator fallbackAnimator = GetComponentInChildren<Animator>(true);
                if (fallbackAnimator != null)
                {
                    Transform candidate = fallbackAnimator.transform;
                    if (candidate.parent != null && candidate.parent != transform)
                        candidate = candidate.parent;

                    modelPivot = candidate;
                }
            }
        }

        if (universalAnimator == null)
        {
            universalAnimator = GetComponent<UniversalCharacterAnimator>();
            if (universalAnimator == null && modelPivot != null)
                universalAnimator = modelPivot.GetComponentInChildren<UniversalCharacterAnimator>(true);
        }

        if (logErrors && modelPivot == null)
        {
            Debug.LogError($"[PlayerMovement] modelPivot nao configurado para '{name}'. Verifique o prefab e o PlayerNetworkSetup.");
        }
    }

    private IEnumerator SetupOwnerInputFallback()
    {
        yield return null; // aguarda Start() dos demais MonoBehaviours

        var pi = GetComponent<PlayerInput>();
        if (pi != null)
        {
            pi.enabled = false;
            yield return null; // libera devices capturados por PlayerInputs remotos
            pi.enabled = true;
            pi.SwitchCurrentActionMap("Player");
        }

        if (inputBridge == null)
            inputBridge = GetComponent<LocalPlayerInputBridge>();
        if (inputBridge == null)
            inputBridge = gameObject.AddComponent<LocalPlayerInputBridge>();

        inputBridge.enabled = true;
        inputBridge.RefreshBindingsAfterPlayerInputReset();

        TryResolveCriticalReferences(false);

        if (TopDownCameraManager.Instance != null)
            TopDownCameraManager.Instance.SetCameraTarget(transform);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[PlayerMovement] SetupOwnerInputFallback: PlayerInput e bridge configurados (PlayerNetworkSetup ausente no prefab).");
    }

    private void SyncOwnerInputFromBridge()
    {
        if (!UsesPolledInput())
            return;

        inputMove = inputBridge.Move;
        inputRun = inputBridge.SprintHeld;
        SetAimState(inputBridge.AimHeld);

        if (inputBridge.ConsumeJumpPressed())
            HandleJumpPressed();
    }

    private bool UsesPolledInput()
    {
        if (inputBridge == null)
            inputBridge = GetComponent<LocalPlayerInputBridge>();

        return inputBridge != null && inputBridge.isActiveAndEnabled;
    }

    private bool TryGetMovementBasis(out Vector3 basisForward, out Vector3 basisRight)
    {
        Transform basisSource = cameraController;
        bool usingCameraController = basisSource != null;

        if (basisSource == null)
            basisSource = modelPivot != null ? modelPivot : transform;

        basisForward = basisSource.forward;
        basisRight = basisSource.right;
        basisForward.y = 0f;
        basisRight.y = 0f;

        if (basisForward.sqrMagnitude <= 0.0001f)
            basisForward = Vector3.forward;
        else
            basisForward.Normalize();

        if (basisRight.sqrMagnitude <= 0.0001f)
            basisRight = Vector3.Cross(Vector3.up, basisForward).normalized;
        else
            basisRight.Normalize();

        return usingCameraController;
    }

    public bool IsGroundedForGameplay(float extraGroundProbeDistance = 0.35f)
    {
        if (controller != null && controller.enabled && controller.isGrounded)
            return true;

        if (IsOwner && isGrounded)
            return true;

        return ProbeGrounded(extraGroundProbeDistance);
    }

    private bool ProbeGrounded(float extraGroundProbeDistance)
    {
        LayerMask probeMask = groundMask.value != 0 ? groundMask : Physics.DefaultRaycastLayers;
        float sphereRadius = 0.2f;
        float probeDistance = Mathf.Max(0.5f, extraGroundProbeDistance);
        Vector3 origin = transform.position + Vector3.up * 0.15f;

        if (controller != null)
        {
            sphereRadius = Mathf.Max(0.15f, controller.radius * 0.9f);
            origin = transform.position + Vector3.up * Mathf.Max(0.1f, sphereRadius);
            probeDistance = Mathf.Max(probeDistance, controller.skinWidth + 0.4f);
        }

        return Physics.SphereCast(
            origin,
            sphereRadius,
            Vector3.down,
            out _,
            probeDistance,
            probeMask,
            QueryTriggerInteraction.Ignore);
    }

    [ServerRpc]
    private void RequestBreakWebSpacePressServerRpc()
    {
        if (!netIsWebTrapped.Value) return;

        SpiderWebDebuffPlayer debuff = GetComponent<SpiderWebDebuffPlayer>();
        if (debuff != null)
        {
            debuff.RegisterSpacePress();
        }
    }
}
