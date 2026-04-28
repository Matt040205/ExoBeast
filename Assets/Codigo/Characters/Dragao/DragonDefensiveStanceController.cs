using System.Collections;
using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Sync;

public class DragonDefensiveStanceController : NetworkBehaviour, IDamageInterceptor
{
    [SerializeField] private float frontalBlockDot = 0.15f;

    public NetworkVariable<bool> IsActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerHealthSystem playerHealth;
    private Coroutine activeRoutine;
    private bool localActiveState;
    private float counterDamage;
    private float counterKnockback;
    private ulong ownerClientId;
    private PlayerHealthSystem ownerHealth;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ResolveDependencies();
        IsActive.OnValueChanged += OnActiveStateChanged;
        ApplyVisualState(IsActive.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsActive.OnValueChanged -= OnActiveStateChanged;
        ApplyVisualState(false);
        base.OnNetworkDespawn();
    }

    public bool ActivateServer(
        float duration,
        float newCounterDamage,
        float newCounterKnockback,
        CommanderAbilityController abilityController,
        Ability ability)
    {
        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkSession && !IsServer)
            return false;

        ResolveDependencies();

        if (abilityController != null)
            abilityController.SetAbilityUsage(ability, true);

        counterDamage = newCounterDamage;
        counterKnockback = newCounterKnockback;
        NetworkGameplayResolver.TryResolveAttackerFromPlayer(gameObject, out ownerClientId, out ownerHealth);

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        SetActiveState(true);
        activeRoutine = StartCoroutine(ActiveRoutine(duration));
        return true;
    }

    public bool TryIntercept(PlayerHealthSystem target, ref DamageRequest request, ref DamageResponse response)
    {
        if (!IsStanceActive() || !request.IsMelee || !IsAttackerInFront(request.Attacker))
            return false;

        response.WasBlocked = true;
        response.ModifiedDamage = 0f;
        CounterAttack(request.Attacker);
        return true;
    }

    private IEnumerator ActiveRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        SetActiveState(false);
        activeRoutine = null;
    }

    private void CounterAttack(Transform attacker)
    {
        if (attacker == null)
            return;

        EnemyHealthSystem enemyHealth = attacker.GetComponent<EnemyHealthSystem>();
        if (enemyHealth != null)
        {
            DamageContext damageContext = new DamageContext(ownerClientId, false, DamageFeedbackMode.AllObservers);
            enemyHealth.ApplyAuthoritativeDamage(counterDamage, 0f, damageContext, ownerHealth);
        }

        EnemyController enemyController = attacker.GetComponent<EnemyController>();
        if (enemyController != null)
        {
            enemyController.ApplySlip();
            enemyController.ApplyKnockback(transform.forward, counterKnockback);
        }
    }

    private bool IsAttackerInFront(Transform attacker)
    {
        if (attacker == null)
            return false;

        Vector3 toAttacker = attacker.position - transform.position;
        toAttacker.y = 0f;

        if (toAttacker.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Dot(transform.forward, toAttacker.normalized) >= frontalBlockDot;
    }

    private bool IsStanceActive()
    {
        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        return isNetworkSession && IsSpawned ? IsActive.Value : localActiveState;
    }

    private void SetActiveState(bool isActive)
    {
        localActiveState = isActive;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned && IsServer)
            IsActive.Value = isActive;

        ApplyVisualState(isActive);
    }

    private void OnActiveStateChanged(bool oldValue, bool newValue)
    {
        ApplyVisualState(newValue);
    }

    private void ApplyVisualState(bool isActive)
    {
        ResolveDependencies();

        if (playerHealth != null)
            playerHealth.isCountering = isActive;
    }

    private void ResolveDependencies()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealthSystem>();
    }
}
