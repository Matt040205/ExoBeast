using UnityEngine;

public class TracoUrbanoLogic : MonoBehaviour
{
    private float _speedMult;
    private float _duration;
    private float _slow;
    private GameObject _prefab;
    private CharacterController _controller;
    private float _spawnTimer;

    public void Initialize(float speedMult, float duration, float slow, GameObject prefab)
    {
        _speedMult = speedMult;
        _duration = duration;
        _slow = slow;
        _prefab = prefab;

        // Pega o CharacterController que já existe no player
        _controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (_controller == null) return;

        // Calcula a velocidade ignorando a gravidade (eixo Y)
        Vector3 horizontalVelocity = _controller.velocity;
        horizontalVelocity.y = 0;

        // Verifica se está se movendo (velocidade > 0.1) e segurando Shift
        bool isMoving = horizontalVelocity.magnitude > 0.1f;
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && isMoving;

        if (isSprinting)
        {
            // Opcional: Se quiser aplicar o boost de velocidade aqui, 
            // teria que acessar a variável runSpeed do PlayerMovement, 
            // mas como ela é publica, daria pra fazer: 
            // GetComponent<PlayerMovement>().currentSpeed *= _speedMult;
            // (Mas cuidado para não acumular o multiplicador infinitamente).

            // Spawna tinta periodicamente
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer > 0.2f) // A cada 0.2s cria uma poça
            {
                SpawnInk();
                _spawnTimer = 0;
            }
        }
    }

    void SpawnInk()
    {
        if (_prefab != null)
        {
            // Cria a tinta exatamente na posição do pé (transform.position)
            // A rotação Quaternion.identity deixa ela reta no chão
            GameObject ink = Instantiate(_prefab, transform.position, Quaternion.identity);

            // Tenta configurar a lógica da tinta (se o prefab tiver script)
            // Exemplo fictício: ink.GetComponent<InkPuddle>().Setup(_slow, _duration);

            Destroy(ink, _duration); // Destroi a tinta após o tempo configurado
        }
    }
}