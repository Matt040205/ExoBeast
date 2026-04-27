using UnityEngine;
using Unity.Cinemachine;
using Unity.Netcode;
using FMODUnity;

/// <summary>
/// â”€â”€ CameraController â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
/// Controla camera Cinemachine de terceira pessoa com mira (aim).
///
///  â–¸ Owner: processa input de mouse, troca entre camera normal e aim
///  â–¸ Remoto: desativa Camera, AudioListener e Cinemachine cameras
///  â–¸ GetAimDirection(): retorna direcao de mira para PlayerShooting
/// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    [Tooltip("Camadas (Layers) que vÃ£o bloquear a cÃ¢mera. Recomendo usar 'Default'.")]
    public LayerMask obstacleMask = -1;

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
            foreach (Camera cam in GetComponentsInChildren<Camera>(true))
                cam.enabled = false;

            foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;

            foreach (StudioListener listener in GetComponentsInChildren<StudioListener>(true))
                listener.enabled = false;

            if (normalCamera != null) normalCamera.enabled = false;
            if (aimCamera != null) aimCamera.enabled = false;

            enabled = false;
            return;
        }

        EnsureLocalAudioRig();
    }

    private void Start()
    {
        normalFollow = normalCamera.GetComponent<CinemachineThirdPersonFollow>();
        aimFollow = aimCamera.GetComponent<CinemachineThirdPersonFollow>();

        if (normalFollow != null) normalMaxDistance = normalFollow.CameraDistance;
        if (aimFollow != null) aimMaxDistance = aimFollow.CameraDistance;

        normalCamera.Priority.Value = PriorityNormal;
        aimCamera.Priority.Value = 5;

        if (IsOwner)
            EnsureLocalAudioRig();
    }

    private void Update()
    {
        if (PauseControl.isPaused)
            return;

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
        if (normalFollow == null || aimFollow == null)
            return;

        Vector3 targetAimOffset = aimFollow.ShoulderOffset;
        targetAimOffset.x = isAiming ? shoulderOffset : 0f;
        aimFollow.ShoulderOffset = Vector3.Lerp(
            aimFollow.ShoulderOffset,
            targetAimOffset,
            aimTransitionSpeed * Time.deltaTime);

        Vector3 targetNormalOffset = normalFollow.ShoulderOffset;
        targetNormalOffset.y = isAiming ? 1.2f : 1.8f;
        normalFollow.ShoulderOffset = Vector3.Lerp(
            normalFollow.ShoulderOffset,
            targetNormalOffset,
            aimTransitionSpeed * Time.deltaTime);
    }

    private void HandleCameraCollision()
    {
        if (normalFollow == null || aimFollow == null)
            return;

        CinemachineThirdPersonFollow activeFollow = isAiming ? aimFollow : normalFollow;
        CinemachineThirdPersonFollow inactiveFollow = isAiming ? normalFollow : aimFollow;
        float targetMaxDistance = isAiming ? aimMaxDistance : normalMaxDistance;

        Vector3 focusPos = transform.position + transform.TransformDirection(activeFollow.ShoulderOffset);
        Vector3 dirToCam = -transform.forward;
        Vector3 desiredCameraPos = focusPos + dirToCam * targetMaxDistance;
        Vector3 safePos = transform.position + Vector3.up;

        Vector3 rayDir = desiredCameraPos - safePos;
        float rayDist = rayDir.magnitude;
        if (rayDist > 0.001f)
            rayDir.Normalize();

        float distance = targetMaxDistance;
        RaycastHit[] hits = Physics.SphereCastAll(safePos, collisionRadius, rayDir, rayDist, obstacleMask);
        float closestFraction = 1f;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.isTrigger)
                continue;

            if (hit.collider.transform.root == transform.root)
                continue;

            float fraction = hit.distance / rayDist;
            if (fraction < closestFraction)
                closestFraction = fraction;
        }

        if (closestFraction < 1f)
        {
            Vector3 hitPos = safePos + rayDir * (rayDist * closestFraction);
            distance = Vector3.Dot(hitPos - focusPos, dirToCam);
        }

        distance = Mathf.Max(distance - collisionRadius, minCameraDistance);
        activeFollow.CameraDistance = Mathf.Lerp(
            activeFollow.CameraDistance,
            distance,
            Time.deltaTime * aimTransitionSpeed);

        inactiveFollow.CameraDistance = isAiming ? normalMaxDistance : aimMaxDistance;
    }

    private void EnsureLocalAudioRig()
    {
        if (!IsOwner)
            return;

        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        Camera activeCamera = null;
        foreach (Camera cam in cameras)
        {
            if (cam != null && cam.isActiveAndEnabled)
            {
                activeCamera = cam;
                break;
            }
        }

        if (activeCamera == null && cameras.Length > 0)
            activeCamera = cameras[0];

        foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
            listener.enabled = activeCamera != null && listener.gameObject == activeCamera.gameObject;

        StudioListener activeStudioListener = null;
        if (activeCamera != null)
        {
            activeStudioListener = activeCamera.GetComponent<StudioListener>();
            if (activeStudioListener == null)
                activeStudioListener = activeCamera.gameObject.AddComponent<StudioListener>();

            activeStudioListener.enabled = true;
        }

        foreach (StudioListener listener in GetComponentsInChildren<StudioListener>(true))
        {
            if (listener != activeStudioListener)
                listener.enabled = false;
        }
    }
}
