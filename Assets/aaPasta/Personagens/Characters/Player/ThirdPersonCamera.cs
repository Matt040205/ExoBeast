using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── ThirdPersonCamera ──────────────────────────────────
/// Camera alternativa com transicao 3a/1a pessoa via zoom scroll.
///
///  ▸ Owner: processa input de mouse e zoom, orbita ao redor do target
///  ▸ Remoto: desativa Camera e AudioListener
///  ▸ Transicao automatica para 1a pessoa quando zoom <= minDistance
/// ─────────────────────────────────────────────────────
/// </summary>
public class ThirdPersonCamera : NetworkBehaviour
{
    public enum CameraState { ThirdPerson, FirstPerson }
    public CameraState currentState = CameraState.ThirdPerson;

    private Vector3 thirdPersonOffset = new Vector3(0.8f, 2.0f, -4f);
    public float defaultDistance = 4f;
    public float minDistance = 0.5f;
    public float maxDistance = 10f;
    public float zoomSensitivity = 1f;

    public Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0.1f);

    public float sensitivity = 2f;
    public float minY = -20f;
    public float maxY = 80f;

    public Transform target;
    private float currentX = 0f;
    private float currentY = 0f;
    private float currentDistance;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            if (TryGetComponent<Camera>(out var cam)) cam.enabled = false;
            if (TryGetComponent<AudioListener>(out var listener)) listener.enabled = false;
            this.enabled = false;
            return;
        }

        // Inicialização apenas para o proprietário
        currentDistance = defaultDistance;
        if (target != null)
        {
            Vector3 direction = transform.position - target.position;
            Quaternion initialRotation = Quaternion.LookRotation(direction);
            currentX = initialRotation.eulerAngles.y;
            currentY = initialRotation.eulerAngles.x;
        }
    }

    void LateUpdate()
    {
        if (!IsOwner || target == null) return;

        if (PauseControl.isPaused) return;

        float zoomInput = Input.GetAxis("Mouse ScrollWheel");
        currentDistance -= zoomInput * zoomSensitivity;
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

        if (currentDistance <= minDistance + 0.1f)
        {
            currentState = CameraState.FirstPerson;
        }
        else
        {
            currentState = CameraState.ThirdPerson;
        }

        currentX += Input.GetAxis("Mouse X") * sensitivity;
        currentY -= Input.GetAxis("Mouse Y") * sensitivity;
        currentY = Mathf.Clamp(currentY, minY, maxY);

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        if (currentState == CameraState.ThirdPerson)
        {
            Vector3 finalOffset = rotation * thirdPersonOffset.normalized * currentDistance;
            transform.position = target.position + finalOffset;

            transform.LookAt(target.position + Vector3.up * 0.2f);
        }
        else if (currentState == CameraState.FirstPerson)
        {
            transform.position = target.position + rotation * firstPersonOffset;
            transform.rotation = rotation;
        }
    }
}
