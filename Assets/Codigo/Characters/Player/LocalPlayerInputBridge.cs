using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridge local do novo Input System para scripts de gameplay.
/// Faz polling do PlayerInput ativo e expoe estados normalizados para movimento e combate.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class LocalPlayerInputBridge : MonoBehaviour
{
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction aimAction;
    private InputAction fireAction;
    private InputAction reloadAction;

    private bool wasJumpHeld;
    private bool wasReloadHeld;
    private bool jumpPressed;
    private bool reloadPressed;

    public Vector2 Move { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool AimHeld { get; private set; }
    public bool FireHeld { get; private set; }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        CacheActions();
    }

    private void OnEnable()
    {
        CacheActions();
        ResetLatchedState();
    }

    private void Update()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerInput == null || !playerInput.enabled)
        {
            ClearState();
            return;
        }

        CacheActions();

        Move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        SprintHeld = sprintAction != null && sprintAction.IsPressed();
        AimHeld = aimAction != null && aimAction.IsPressed();
        FireHeld = fireAction != null && fireAction.IsPressed();

        bool jumpHeldNow = jumpAction != null && jumpAction.IsPressed();
        if (jumpHeldNow && !wasJumpHeld)
            jumpPressed = true;
        wasJumpHeld = jumpHeldNow;

        bool reloadHeldNow = reloadAction != null && reloadAction.IsPressed();
        if (reloadHeldNow && !wasReloadHeld)
            reloadPressed = true;
        wasReloadHeld = reloadHeldNow;
    }

    public bool ConsumeJumpPressed()
    {
        if (!jumpPressed)
            return false;

        jumpPressed = false;
        return true;
    }

    public bool ConsumeReloadPressed()
    {
        if (!reloadPressed)
            return false;

        reloadPressed = false;
        return true;
    }

    private void CacheActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        moveAction ??= playerInput.actions.FindAction("Move", throwIfNotFound: false);
        sprintAction ??= playerInput.actions.FindAction("Sprint", throwIfNotFound: false);
        jumpAction ??= playerInput.actions.FindAction("Jump", throwIfNotFound: false);
        aimAction ??= playerInput.actions.FindAction("Aim", throwIfNotFound: false);
        fireAction ??= playerInput.actions.FindAction("Attack", throwIfNotFound: false);
        reloadAction ??= playerInput.actions.FindAction("Reload", throwIfNotFound: false);
    }

    private void ResetLatchedState()
    {
        wasJumpHeld = false;
        wasReloadHeld = false;
        jumpPressed = false;
        reloadPressed = false;
    }

    private void ClearState()
    {
        Move = Vector2.zero;
        SprintHeld = false;
        AimHeld = false;
        FireHeld = false;
        ResetLatchedState();
    }
}
