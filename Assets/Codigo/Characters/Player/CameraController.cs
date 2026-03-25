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

        normalCamera.Priority.Value = PriorityNormal;
        aimCamera.Priority.Value = 5;
    }

    private void Update()
    {
        HandleCameraRotation();
        HandleAimToggle();
        UpdateCameraOffsets();
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
}