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
    public LayerMask groundLayerMask = 1; // Default layer

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

        // Passiva deve dropar tinta apenas correndo (sprint)
        if (!isSprinting)
            return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer > 0.2f)
        {
            SpawnInk();
            _spawnTimer = 0f;
        }
    }

    private Vector3 GetGroundPosition()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
        
        Vector3 bestPoint = transform.position;
        float highestY = -9999f;
        bool found = false;

        foreach (var hit in hits)
        {
            // Ignora o proprio jogador
            if (hit.collider.transform.root != transform.root && !hit.collider.isTrigger)
            {
                if (hit.point.y > highestY)
                {
                    highestY = hit.point.y;
                    bestPoint = hit.point;
                    found = true;
                }
            }
        }

        if (found) return bestPoint;

        if (_controller != null)
            return new Vector3(transform.position.x, _controller.bounds.min.y, transform.position.z);

        return transform.position;
    }

    void SpawnInk()
    {
        if (_prefab == null)
            return;

        Vector3 spawnPos = GetGroundPosition();

        // Para a gota olhar na direção do movimento (já que o root do player não roda)
        Vector3 horizontalVelocity = _controller != null ? _controller.velocity : Vector3.zero;
        horizontalVelocity.y = 0;
        Quaternion spawnRot = horizontalVelocity.sqrMagnitude > 0.01f 
            ? Quaternion.LookRotation(horizontalVelocity.normalized) 
            : transform.rotation;

        GameObject ink = Instantiate(_prefab, spawnPos, spawnRot);
        
        CaminhoInkController inkController = ink.GetComponent<CaminhoInkController>();
        if (inkController != null)
            inkController.SetSphereActive(false); // Passiva não tem a esfera, apenas o rastro

        Destroy(ink, _duration);
    }
}
