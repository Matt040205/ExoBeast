using UnityEngine;
using Unity.Cinemachine;
using Unity.Netcode;

/// <summary>
/// ── CameraController ───────────────────────────────────
/// Controla camera Cinemachine de terceira pessoa com mira (aim).
///
///  ▸ Owner: processa input de mouse, troca entre camera normal e aim
///  ▸ Remoto: desativa Camera, AudioListener e Cinemachine cameras
///  ▸ GetAimDirection(): retorna direcao de mira para PlayerShooting
/// ─────────────────────────────────────────────────────
/// </summary>
public class CameraController : NetworkBehaviour
{
    [Header("Camera Settings")]
    public CinemachineCamera normalCamera;
    public CinemachineCamera aimCamera;
    public float shoulderOffset = 1.16f;
    public float aimTransitionSpeed = 15f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public float verticalAngleLimit = 80f;

    [Header("Camera Collision")]
    public float normalMaxDistance = 3f;
    public float aimMaxDistance = 1.5f;
    public float minCameraDistance = 0.5f;
    public float collisionRadius = 0.2f;
    [Tooltip("Camadas (Layers) que vão bloquear a câmera. Recomendo usar 'Default'.")]
    public LayerMask obstacleMask = -1; // -1 significa todas as layers

    public bool isAiming { get; private set; }

    private CinemachineThirdPersonFollow normalFollow;
    private CinemachineThirdPersonFollow aimFollow;
    private float cameraRotationX;
    private float cameraRotationY;

    private const int PriorityNormal = 10;
    private const int PriorityAim = 15;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            // Desativar camera e audio de jogadores remotos
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cam.enabled = false;

            var listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;

            // Desativar Cinemachine cameras se existirem
            if (normalCamera != null) normalCamera.enabled = false;
            if (aimCamera != null) aimCamera.enabled = false;

            this.enabled = false; // Nao processar Update/LateUpdate
            return;
        }
    }

    private void Start()
    {
        normalFollow = normalCamera.GetComponent<CinemachineThirdPersonFollow>();
        aimFollow = aimCamera.GetComponent<CinemachineThirdPersonFollow>();

        if (normalFollow != null) normalMaxDistance = normalFollow.CameraDistance;
        if (aimFollow != null) aimMaxDistance = aimFollow.CameraDistance;

        normalCamera.Priority.Value = PriorityNormal;
        aimCamera.Priority.Value = 5;
    }

    private void Update()
    {
        if (PauseControl.isPaused) return;

        HandleCameraRotation();
        HandleAimToggle();
        UpdateCameraOffsets();
        HandleCameraCollision();
    }

    public Vector3 GetAimDirection()
    {
        return isAiming ? aimCamera.transform.forward : normalCamera.transform.forward;
    }

    private void HandleCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * -1;

        cameraRotationX += mouseX;
        cameraRotationY += mouseY;
        cameraRotationY = Mathf.Clamp(cameraRotationY, -verticalAngleLimit, verticalAngleLimit);

        transform.rotation = Quaternion.Euler(cameraRotationY, cameraRotationX, 0);
    }

    private void HandleAimToggle()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isAiming = true;
            aimCamera.Priority.Value = PriorityAim;
        }
        if (Input.GetMouseButtonUp(1))
        {
            isAiming = false;
            aimCamera.Priority.Value = 5;
        }
    }

    private void UpdateCameraOffsets()
    {
        if (normalFollow == null || aimFollow == null) return;

        Vector3 targetAimOffset = aimFollow.ShoulderOffset;
        targetAimOffset.x = isAiming ? shoulderOffset : 0;
        aimFollow.ShoulderOffset = Vector3.Lerp(aimFollow.ShoulderOffset, targetAimOffset, aimTransitionSpeed * Time.deltaTime);

        Vector3 targetNormalOffset = normalFollow.ShoulderOffset;
        targetNormalOffset.y = isAiming ? 1.2f : 1.8f;
        normalFollow.ShoulderOffset = Vector3.Lerp(normalFollow.ShoulderOffset, targetNormalOffset, aimTransitionSpeed * Time.deltaTime);
    }

    private void HandleCameraCollision()
    {
        if (normalFollow == null || aimFollow == null) return;

        CinemachineThirdPersonFollow activeFollow = isAiming ? aimFollow : normalFollow;
        CinemachineThirdPersonFollow inactiveFollow = isAiming ? normalFollow : aimFollow;
        float targetMaxDistance = isAiming ? aimMaxDistance : normalMaxDistance;

        // Ponto âncora padrão que a Cinemachine usa para focar
        Vector3 focusPos = transform.position + transform.TransformDirection(activeFollow.ShoulderOffset);
        
        // A direção para trás da câmera
        Vector3 dirToCam = -transform.forward;

        // A posição alvo final onde a câmera tentará ficar
        Vector3 desiredCameraPos = focusPos + dirToCam * targetMaxDistance;

        // PONTO SEGURO: Projetamos o raio do centro do jogador do peito p/ evitar que inicie dentro de uma parede
        Vector3 safePos = transform.position + Vector3.up * 1f;

        // Direção e distância isolada do peito do jogador até a posição da câmera
        Vector3 rayDir = desiredCameraPos - safePos;
        float rayDist = rayDir.magnitude;
        if (rayDist > 0.001f) rayDir.Normalize();

        float distance = targetMaxDistance;

        // Um SphereCast partindo do "peito" da personagem (onde sabemos com 100% que estamos fora de paredes)
        RaycastHit[] hits = Physics.SphereCastAll(safePos, collisionRadius, rayDir, rayDist, obstacleMask);
        float closestFraction = 1f;

        foreach (var hit in hits)
        {
            if (hit.collider.isTrigger) continue; // ignora triggers
            if (hit.collider.transform.root == this.transform.root) continue; // ignora o próprio player

            float fraction = hit.distance / rayDist;
            if (fraction < closestFraction)
            {
                closestFraction = fraction;
            }
        }

        if (closestFraction < 1f)
        {
            // Bateu em algo! O ponto de colisão seguro antes de atravessar a construção:
            Vector3 hitPos = safePos + rayDir * (rayDist * closestFraction);

            // Agora nós projetamos esse hitPos de volta para o eixo linear onde a câmera anda (dirToCam)
            distance = Vector3.Dot(hitPos - focusPos, dirToCam);
        }

        distance = Mathf.Max(distance - collisionRadius, minCameraDistance);

        // Suaviza a colisão para não tremular
        activeFollow.CameraDistance = Mathf.Lerp(activeFollow.CameraDistance, distance, Time.deltaTime * aimTransitionSpeed);
        
        // Mantém a câmera secundária restaurada
        inactiveFollow.CameraDistance = isAiming ? normalMaxDistance : aimMaxDistance;
    }
}