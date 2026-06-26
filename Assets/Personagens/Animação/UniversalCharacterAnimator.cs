using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public static class CharacterActionID
{
    public const int Jump = 1;
    public const int Attack = 2;
    public const int Shoot = 3;
    public const int Reload = 4;
    public const int CacadoraUltimate = 5;
    public const int Heal = 6;
    public const int Slip = 7;
    public const int Dead = 8;
}

[DisallowMultipleComponent]
public class UniversalCharacterAnimator : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;

    [Header("Airborne Settings")]
    [SerializeField] private float landingRaycastDistance = 1.2f;
    [SerializeField] private LayerMask groundMask;

    // Animator Parameter Hashes (Cache)
    private static readonly int MovementSpeedHash = Animator.StringToHash("MovementSpeed");
    private static readonly int AimMoveXHash = Animator.StringToHash("AimMoveX");
    private static readonly int AimMoveYHash = Animator.StringToHash("AimMoveY");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int YVelocityHash = Animator.StringToHash("yVelocity");
    private static readonly int IsAboutToLandHash = Animator.StringToHash("isAboutToLand");
    private static readonly int IsAimingHash = Animator.StringToHash("isAiming");
    private static readonly int ActionIDHash = Animator.StringToHash("ActionID");
    private static readonly int ActionTriggerHash = Animator.StringToHash("ActionTrigger");
    private static readonly int ContinuousStateHash = Animator.StringToHash("ContinuousState");
    private static readonly int AttackSpeedMultiplierHash = Animator.StringToHash("AttackSpeedMultiplier");
    private static readonly int ReloadSpeedMultiplierHash = Animator.StringToHash("ReloadSpeedMultiplier");

    private void Awake()
    {
        EnsureComponentsResolved();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        EnsureComponentsResolved();
    }

    private void EnsureComponentsResolved()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
        if (networkAnimator == null)
        {
            networkAnimator = GetComponent<NetworkAnimator>();
            if (networkAnimator == null)
            {
                networkAnimator = GetComponentInChildren<NetworkAnimator>(true);
            }
        }
    }

    /// <summary>
    /// Updates character movement parameters in the Animator.
    /// </summary>
    public void UpdateMovement(float speed, float aimMoveX = 0f, float aimMoveY = 0f, float dampTime = 0.1f)
    {
        if (animator == null) return;

        if (dampTime > 0f)
        {
            animator.SetFloat(MovementSpeedHash, speed, dampTime, Time.deltaTime);
            animator.SetFloat(AimMoveXHash, aimMoveX, dampTime, Time.deltaTime);
            animator.SetFloat(AimMoveYHash, aimMoveY, dampTime, Time.deltaTime);
        }
        else
        {
            animator.SetFloat(MovementSpeedHash, speed);
            animator.SetFloat(AimMoveXHash, aimMoveX);
            animator.SetFloat(AimMoveYHash, aimMoveY);
        }
    }

    /// <summary>
    /// Updates airborne states including ground check and landing prediction.
    /// </summary>
    public void UpdateAirborne(bool isGrounded, float verticalVelocity)
    {
        if (animator == null) return;

        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetFloat(YVelocityHash, verticalVelocity);

        bool aboutToLand = false;
        if (!isGrounded && verticalVelocity < 0f)
        {
            aboutToLand = Physics.Raycast(transform.position, Vector3.down, landingRaycastDistance, groundMask);
        }
        else if (isGrounded)
        {
            aboutToLand = true;
        }

        animator.SetBool(IsAboutToLandHash, aboutToLand);
    }

    /// <summary>
    /// Sets the aiming state of the character.
    /// </summary>
    public void SetAiming(bool isAiming)
    {
        if (animator == null) return;
        animator.SetBool(IsAimingHash, isAiming);
    }

    /// <summary>
    /// Sets the attack speed multiplier.
    /// </summary>
    public void SetAttackSpeedMultiplier(float multiplier)
    {
        if (animator == null) return;
        animator.SetFloat(AttackSpeedMultiplierHash, multiplier);
    }

    /// <summary>
    /// Sets the reload speed multiplier.
    /// </summary>
    public void SetReloadSpeedMultiplier(float multiplier)
    {
        if (animator == null) return;
        animator.SetFloat(ReloadSpeedMultiplierHash, multiplier);
    }

    /// <summary>
    /// Triggers a specific action (attack, jump, etc.) using actionID and a universal trigger.
    /// </summary>
    public void TriggerAction(int actionID)
    {
        if (animator == null) return;

        // Set the ID locally first
        animator.SetInteger(ActionIDHash, actionID);

        // Sync trigger over network if active, otherwise trigger locally
        if (networkAnimator != null && networkAnimator.enabled && networkAnimator.IsSpawned)
        {
            networkAnimator.SetTrigger("ActionTrigger");
        }
        else
        {
            animator.SetTrigger(ActionTriggerHash);
        }
    }

    /// <summary>
    /// Sets a continuous/looping state (e.g., meditation, special focus).
    /// </summary>
    public void SetContinuousState(int stateID)
    {
        if (animator == null) return;
        animator.SetInteger(ContinuousStateHash, stateID);
    }

    /// <summary>
    /// Rebinds the animator to recalculate position and reset state.
    /// </summary>
    public void Rebind()
    {
        if (animator == null) return;
        animator.Rebind();
    }

    /// <summary>
    /// Resets the action trigger to prevent queued animations.
    /// </summary>
    public void ResetActionTrigger()
    {
        if (animator == null) return;
        animator.ResetTrigger(ActionTriggerHash);
    }
}
