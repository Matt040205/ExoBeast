using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TPSMovementAndCamera : MonoBehaviour
{
    [Header("Configurações da Câmara")]
    public Transform cameraTransform;
    public float distance = 5f;
    public float height = 1.5f;
    public float mouseSensitivity = 0.2f;

    [Header("Movimento e Dash")]
    public float moveSpeed = 6f;
    public float dashSpeed = 25f;
    public float dashDuration = 0.2f;

    private CharacterController controller;
    private MeshTrail trailScript;
    private bool isDashing = false;
    private float camPitch = 0f;
    private float camYaw = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        trailScript = GetComponent<MeshTrail>();

        if (trailScript == null)
        {
            Debug.LogError("ERRO: O script MeshTrail não está no mesmo objeto que este controlador de movimento!");
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        HandleCamera();

        if (!isDashing)
        {
            HandleMovement();

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StartCoroutine(PerformDash());
            }
        }
    }

    private void HandleCamera()
    {
        if (cameraTransform == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        camYaw += mouseDelta.x * mouseSensitivity;
        camPitch -= mouseDelta.y * mouseSensitivity;
        camPitch = Mathf.Clamp(camPitch, -20f, 60f);

        cameraTransform.rotation = Quaternion.Euler(camPitch, camYaw, 0f);
        cameraTransform.position = transform.position + (Vector3.up * height) - (cameraTransform.forward * distance);
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1;
        if (Keyboard.current.sKey.isPressed) input.y -= 1;
        if (Keyboard.current.dKey.isPressed) input.x += 1;
        if (Keyboard.current.aKey.isPressed) input.x -= 1;

        Vector3 inputDir = new Vector3(input.x, 0, input.y).normalized;

        if (inputDir.magnitude >= 0.1f && cameraTransform != null)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);
        }

        controller.Move(Vector3.down * 9.81f * Time.deltaTime);
    }

    private IEnumerator PerformDash()
    {
        isDashing = true;

        if (trailScript != null)
        {
            trailScript.SpawnGhostAt(transform.position, transform.rotation, transform);
        }

        float startTime = Time.time;
        while (Time.time < startTime + dashDuration)
        {
            controller.Move(transform.forward * dashSpeed * Time.deltaTime);
            yield return null;
        }

        isDashing = false;
    }
}
