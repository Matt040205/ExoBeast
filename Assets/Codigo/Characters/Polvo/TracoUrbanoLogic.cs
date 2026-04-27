using UnityEngine;
using Unity.Netcode;

public class TracoUrbanoLogic : MonoBehaviour
{
    private float _speedMult;
    private float _duration;
    private float _slow;
    private GameObject _prefab;
    private CharacterController _controller;
    private LocalPlayerInputBridge _inputBridge;
    private NetworkObject _networkObject;
    private float _spawnTimer;

    public void Initialize(float speedMult, float duration, float slow, GameObject prefab)
    {
        _speedMult = speedMult;
        _duration = duration;
        _slow = slow;
        _prefab = prefab;
        _controller = GetComponent<CharacterController>();
        _inputBridge = GetComponent<LocalPlayerInputBridge>();
        _networkObject = GetComponent<NetworkObject>();
    }

    void Update()
    {
        if (_controller == null)
            return;

        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;

        if (_inputBridge == null)
            _inputBridge = GetComponent<LocalPlayerInputBridge>();

        if (_inputBridge == null || !_inputBridge.isActiveAndEnabled)
            return;

        Vector3 horizontalVelocity = _controller.velocity;
        horizontalVelocity.y = 0;

        bool isMoving = horizontalVelocity.magnitude > 0.1f;
        bool isSprinting = _inputBridge.SprintHeld && isMoving;

        if (!isSprinting)
            return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer > 0.2f)
        {
            SpawnInk();
            _spawnTimer = 0f;
        }
    }

    void SpawnInk()
    {
        if (_prefab == null)
            return;

        GameObject ink = Instantiate(_prefab, transform.position, Quaternion.identity);
        Destroy(ink, _duration);
    }
}
