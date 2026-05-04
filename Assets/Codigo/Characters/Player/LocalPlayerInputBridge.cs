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
    private InputActionAsset cachedActionsAsset;
    private string cachedActionMapName;

    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction aimAction;
    private InputAction fireAction;
    private InputAction reloadAction;
    private InputAction buildAction;
    private InputAction ability1Action;
    private InputAction ability2Action;
    private InputAction ultimateAction;

    private bool wasJumpHeld;
    private bool wasReloadHeld;
    private bool wasBuildHeld;
    private bool wasAbility1Held;
    private bool wasAbility2Held;
    private bool wasUltimateHeld;
    private bool wasFireHeld;
    private bool jumpPressed;
    private bool reloadPressed;
    private bool buildPressed;
    private bool ability1Pressed;
    private bool ability2Pressed;
    private bool ultimatePressed;
    private bool meleeAttackPressed;
    private bool firePressed;

    public Vector2 Move { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool AimHeld { get; private set; }
    public bool FireHeld { get; private set; }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        RefreshBindingsAfterPlayerInputReset();
    }

    private void OnEnable()
    {
        // Limpa referências antes de re-cachear: o PlayerInput pode ter passado por um
        // ciclo disable→enable que invalida o estado interno das actions.
        RefreshBindingsAfterPlayerInputReset();
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

        EnsureBindingsMatchCurrentPlayerInput();
        CacheActions();

        Move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        SprintHeld = sprintAction != null && sprintAction.IsPressed();
        AimHeld = aimAction != null && aimAction.IsPressed();
        FireHeld = fireAction != null && fireAction.IsPressed();

        bool fireHeldNow = FireHeld;
        if (fireHeldNow && !wasFireHeld) 
        {
            meleeAttackPressed = true;
            firePressed = true;
        }
        wasFireHeld = fireHeldNow;

        bool jumpHeldNow = jumpAction != null && jumpAction.IsPressed();
        if (jumpHeldNow && !wasJumpHeld)
            jumpPressed = true;
        wasJumpHeld = jumpHeldNow;

        bool reloadHeldNow = reloadAction != null && reloadAction.IsPressed();
        if (reloadHeldNow && !wasReloadHeld)
            reloadPressed = true;
        wasReloadHeld = reloadHeldNow;

        bool buildHeldNow = buildAction != null && buildAction.IsPressed();
        if (buildHeldNow && !wasBuildHeld)
            buildPressed = true;
        wasBuildHeld = buildHeldNow;

        bool ability1HeldNow = ability1Action != null && ability1Action.IsPressed();
        if (ability1HeldNow && !wasAbility1Held)
            ability1Pressed = true;
        wasAbility1Held = ability1HeldNow;

        bool ability2HeldNow = ability2Action != null && ability2Action.IsPressed();
        if (ability2HeldNow && !wasAbility2Held)
            ability2Pressed = true;
        wasAbility2Held = ability2HeldNow;

        bool ultimateHeldNow = ultimateAction != null && ultimateAction.IsPressed();
        if (ultimateHeldNow && !wasUltimateHeld)
            ultimatePressed = true;
        wasUltimateHeld = ultimateHeldNow;
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

    public bool ConsumeBuildPressed()
    {
        if (!buildPressed)
            return false;

        buildPressed = false;
        return true;
    }

    public bool ConsumeAbility1Pressed()
    {
        if (!ability1Pressed)
            return false;

        ability1Pressed = false;
        return true;
    }

    public bool ConsumeAbility2Pressed()
    {
        if (!ability2Pressed)
            return false;

        ability2Pressed = false;
        return true;
    }

    public bool ConsumeUltimatePressed()
    {
        if (!ultimatePressed)
            return false;

        ultimatePressed = false;
        return true;
    }

    public bool ConsumeMeleeAttackPressed()
    {
        if (!meleeAttackPressed)
            return false;

        meleeAttackPressed = false;
        return true;
    }

    public bool ConsumeFirePressed()
    {
        if (!firePressed)
            return false;

        firePressed = false;
        return true;
    }

    public void RefreshBindingsAfterPlayerInputReset()
    {
        playerInput = GetComponent<PlayerInput>();
        InvalidateCachedActions();
        CacheActions();
        ResetLatchedState();
    }

    private void EnsureBindingsMatchCurrentPlayerInput()
    {
        InputActionAsset currentActionsAsset = playerInput != null ? playerInput.actions : null;
        string currentActionMapName = playerInput?.currentActionMap != null
            ? playerInput.currentActionMap.name
            : string.Empty;

        if (currentActionsAsset == cachedActionsAsset &&
            currentActionMapName == cachedActionMapName)
        {
            return;
        }

        InvalidateCachedActions();
        CacheActions();
        ResetLatchedState();
    }

    private void InvalidateCachedActions()
    {
        cachedActionsAsset = playerInput != null ? playerInput.actions : null;
        cachedActionMapName = playerInput?.currentActionMap != null
            ? playerInput.currentActionMap.name
            : string.Empty;

        moveAction = null;
        sprintAction = null;
        jumpAction = null;
        aimAction = null;
        fireAction = null;
        reloadAction = null;
        buildAction = null;
        ability1Action = null;
        ability2Action = null;
        ultimateAction = null;
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
        buildAction ??= playerInput.actions.FindAction("Build", throwIfNotFound: false);
        ability1Action ??= playerInput.actions.FindAction("Ability1", throwIfNotFound: false);
        ability2Action ??= playerInput.actions.FindAction("Ability2", throwIfNotFound: false);
        ultimateAction ??= playerInput.actions.FindAction("Ultimate", throwIfNotFound: false);
    }

    private void ResetLatchedState()
    {
        wasJumpHeld = false;
        wasReloadHeld = false;
        wasBuildHeld = false;
        wasAbility1Held = false;
        wasAbility2Held = false;
        wasUltimateHeld = false;
        wasFireHeld = false;
        jumpPressed = false;
        reloadPressed = false;
        buildPressed = false;
        ability1Pressed = false;
        ability2Pressed = false;
        ultimatePressed = false;
        meleeAttackPressed = false;
        firePressed = false;
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
