using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyStatusController : MonoBehaviour
{
    private const float DefaultSlipDuration = 1.5f;
    private const float DefaultKnockbackDuration = 0.2f;

    private NavMeshAgent agent;
    private Animator animator;

    private Coroutine slowCoroutine;
    private Coroutine slipCoroutine;
    private Coroutine stunCoroutine;
    private Coroutine knockbackCoroutine;
    private Coroutine knockUpCoroutine;

    private int movementLocks;
    private int manualMovementClaims;
    private float speedModifier = 1f;

    public bool IsBlinded { get; private set; }
    public bool CanMove => movementLocks <= 0;
    public float SpeedModifier => speedModifier;

    public void Initialize(NavMeshAgent navMeshAgent, Animator enemyAnimator)
    {
        agent = navMeshAgent;
        animator = enemyAnimator;
        ResetState();
    }

    public void ResetState()
    {
        StopAllCoroutines();
        slowCoroutine = null;
        slipCoroutine = null;
        stunCoroutine = null;
        knockbackCoroutine = null;
        knockUpCoroutine = null;
        movementLocks = 0;
        manualMovementClaims = 0;
        speedModifier = 1f;
        IsBlinded = false;
        SyncAgentState();
        SetManualMovement(false);
    }

    public void SetBlinded(bool isBlinded)
    {
        IsBlinded = isBlinded;
    }

    public void ApplySlow(float percentage, float duration)
    {
        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);

        slowCoroutine = StartCoroutine(SlowRoutine(Mathf.Clamp01(percentage), duration));
    }

    public void SetPersistentSlow(float percentage)
    {
        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);

        speedModifier = 1f - Mathf.Clamp01(percentage);
        slowCoroutine = null;
    }

    public void ClearSlow()
    {
        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);

        speedModifier = 1f;
        slowCoroutine = null;
    }

    public void ApplyStun(float duration)
    {
        if (stunCoroutine != null)
            StopCoroutine(stunCoroutine);

        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    public void ApplySlip()
    {
        if (slipCoroutine == null)
            slipCoroutine = StartCoroutine(SlipRoutine());
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
            SetManualMovement(false);
            SetMovementLocked(false);
        }

        knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction, force));
    }

    public void ApplyKnockUp(float duration, float height)
    {
        if (knockUpCoroutine != null)
        {
            StopCoroutine(knockUpCoroutine);
            SetManualMovement(false);
            SetMovementLocked(false);
        }

        knockUpCoroutine = StartCoroutine(KnockUpRoutine(duration, height));
    }

    private IEnumerator SlowRoutine(float percentage, float duration)
    {
        speedModifier = 1f - percentage;
        yield return new WaitForSeconds(duration);
        speedModifier = 1f;
        slowCoroutine = null;
    }

    private IEnumerator StunRoutine(float duration)
    {
        SetMovementLocked(true);
        yield return new WaitForSeconds(duration);
        SetMovementLocked(false);
        stunCoroutine = null;
    }

    private IEnumerator SlipRoutine()
    {
        SetMovementLocked(true);

        if (animator != null)
            animator.SetTrigger("Slip");

        yield return new WaitForSeconds(DefaultSlipDuration);

        SetMovementLocked(false);
        slipCoroutine = null;
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force)
    {
        SetMovementLocked(true);
        SetManualMovement(true);

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction.normalized * Mathf.Max(force, 0f) * 0.15f;
        float elapsed = 0f;

        while (elapsed < DefaultKnockbackDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / DefaultKnockbackDuration);
            ApplyWorldPosition(Vector3.Lerp(startPosition, targetPosition, normalized));
            yield return null;
        }

        SetManualMovement(false);
        SetMovementLocked(false);
        knockbackCoroutine = null;
    }

    private IEnumerator KnockUpRoutine(float duration, float height)
    {
        SetMovementLocked(true);
        SetManualMovement(true);

        Vector3 basePosition = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float verticalOffset = Mathf.Sin(normalized * Mathf.PI) * Mathf.Max(height, 0f);
            ApplyWorldPosition(basePosition + Vector3.up * verticalOffset);
            yield return null;
        }

        ApplyWorldPosition(basePosition);
        SetManualMovement(false);
        SetMovementLocked(false);
        knockUpCoroutine = null;
    }

    private void ApplyWorldPosition(Vector3 desiredPosition)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.updatePosition)
        {
            agent.Warp(desiredPosition);
        }
        else
        {
            transform.position = desiredPosition;
        }
    }

    private void SetManualMovement(bool enabled)
    {
        manualMovementClaims = Mathf.Max(0, manualMovementClaims + (enabled ? 1 : -1));

        if (agent == null || !agent.enabled)
            return;

        bool shouldUseManualMovement = manualMovementClaims > 0;
        agent.updatePosition = !shouldUseManualMovement;
        agent.updateRotation = !shouldUseManualMovement;

        if (!shouldUseManualMovement && agent.isOnNavMesh)
        {
            agent.Warp(transform.position);
            agent.nextPosition = transform.position;
        }
    }

    private void SetMovementLocked(bool isLocked)
    {
        movementLocks = Mathf.Max(0, movementLocks + (isLocked ? 1 : -1));
        SyncAgentState();
    }

    private void SyncAgentState()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = movementLocks > 0;
    }
}
