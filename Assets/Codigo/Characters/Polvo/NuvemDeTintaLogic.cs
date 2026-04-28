using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(NetworkObject))]
public class NuvemDeTintaLogic : NetworkBehaviour
{
    [Tooltip("Quanto da velocidade original vai SOBRAR. Ex: 0.6 mantem 60% da velocidade.")]
    public float slowFactor = 0.6f;

    private readonly Dictionary<EnemyController, int> affectedEnemies = new Dictionary<EnemyController, int>();

    private readonly NetworkVariable<float> cloudRadius = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> lifetimeSeconds = new NetworkVariable<float>(
        4f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private float pendingDuration = 4f;
    private float pendingRadius = 1f;
    private bool lifetimeStarted;

    public void Setup(float duration, float radius)
    {
        pendingDuration = duration;
        pendingRadius = radius;
        ApplyRadius(radius);

        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!isNetworkSession)
            StartLifetimeCountdown(duration, networked: false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        cloudRadius.OnValueChanged += OnRadiusChanged;
        lifetimeSeconds.OnValueChanged += OnLifetimeChanged;

        if (IsServer)
        {
            cloudRadius.Value = pendingRadius;
            lifetimeSeconds.Value = pendingDuration;
            StartLifetimeCountdown(lifetimeSeconds.Value, networked: true);
        }

        ApplyRadius(cloudRadius.Value > 0f ? cloudRadius.Value : pendingRadius);
    }

    public override void OnNetworkDespawn()
    {
        cloudRadius.OnValueChanged -= OnRadiusChanged;
        lifetimeSeconds.OnValueChanged -= OnLifetimeChanged;
        base.OnNetworkDespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!ShouldProcessGameplay() || !other.CompareTag("Enemy"))
            return;

        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy == null)
            return;

        if (!affectedEnemies.ContainsKey(enemy))
            affectedEnemies[enemy] = 0;

        affectedEnemies[enemy]++;
        if (affectedEnemies[enemy] > 1)
            return;

        enemy.AplicarDesaceleracao(1f - slowFactor);
        enemy.SetBlinded(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!ShouldProcessGameplay() || !other.CompareTag("Enemy"))
            return;

        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy == null || !affectedEnemies.ContainsKey(enemy))
            return;

        affectedEnemies[enemy]--;
        if (affectedEnemies[enemy] > 0)
            return;

        ClearEnemyStatus(enemy);
        affectedEnemies.Remove(enemy);
    }

    private new void OnDestroy()
    {
        foreach (EnemyController enemy in affectedEnemies.Keys)
        {
            if (enemy != null)
                ClearEnemyStatus(enemy);
        }

        affectedEnemies.Clear();
    }

    private void StartLifetimeCountdown(float duration, bool networked)
    {
        if (lifetimeStarted)
            return;

        lifetimeStarted = true;
        StartCoroutine(LifetimeRoutine(duration, networked));
    }

    private IEnumerator LifetimeRoutine(float duration, bool networked)
    {
        yield return new WaitForSeconds(duration);

        if (networked && IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    private bool ShouldProcessGameplay()
    {
        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        return !isNetworkSession || IsServer;
    }

    private void ApplyRadius(float radius)
    {
        transform.localScale = Vector3.one * radius;
    }

    private void ClearEnemyStatus(EnemyController enemy)
    {
        enemy.RemoverDesaceleracao();
        enemy.SetBlinded(false);
    }

    private void OnRadiusChanged(float oldValue, float newValue)
    {
        ApplyRadius(newValue);
    }

    private void OnLifetimeChanged(float oldValue, float newValue)
    {
        if (IsServer)
            StartLifetimeCountdown(newValue, networked: true);
    }
}
