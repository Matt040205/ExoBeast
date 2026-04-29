using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GroundSlashShooter : MonoBehaviour
{
    [Header("Referências")]
    public Camera cam;
    public GameObject projectile;
    public Transform firePoint;

    [Header("Configurações do Cone (Ultimate)")]
    [Tooltip("Quantidade de meias-luas (ex: 3 ou 5)")]
    public int numberOfSlashes = 3;

    [Tooltip("Ângulo total da área triangular/cone em graus")]
    public float spreadAngle = 60f;

    void Update()
    {
        // Checa se o mouse existe e se o botão esquerdo foi clicado neste exato frame
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ShootProjectile();
        }
    }

    void ShootProjectile()
    {
        // Cria um raio partindo do exato centro da tela da câmera
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        Vector3 destination;

        // Verifica se o raio bateu em algo no cenário
        if (Physics.Raycast(ray, out hit))
        {
            destination = hit.point;
        }
        else
        {
            // Se atirou pro céu, mira num ponto muito distante
            destination = ray.GetPoint(1000);
        }

        InstantiateConeProjectiles(destination);
    }

    void InstantiateConeProjectiles(Vector3 destination)
    {
        // Calcula a direção central (para onde a câmera mirou), ignorando a altura (Y)
        Vector3 direction = destination - firePoint.position;
        direction.y = 0;

        // Descobre qual é a rotação base desse tiro central
        Quaternion baseRotation = Quaternion.LookRotation(direction);

        // Se for só 1, atira reto e sai da função
        if (numberOfSlashes <= 1)
        {
            CriarSlash(baseRotation);
            return;
        }

        // Matemática do cone
        float startingAngle = -spreadAngle / 2f;
        float angleStep = spreadAngle / (numberOfSlashes - 1);

        for (int i = 0; i < numberOfSlashes; i++)
        {
            // Calcula o ângulo deste projétil específico
            float currentAngle = startingAngle + (angleStep * i);

            // Rotaciona a partir da direção base do tiro
            Quaternion rotationOffset = Quaternion.Euler(0f, currentAngle, 0f);
            Quaternion finalRotation = baseRotation * rotationOffset;

            // Instancia passando a rotação final calculada
            CriarSlash(finalRotation);
        }
    }

    void CriarSlash(Quaternion rotacao)
    {
        // Cria o efeito no ponto de disparo
        GameObject slashObj = Instantiate(projectile, firePoint.position, rotacao);

        // Pega os componentes do recém-criado prefab
        GroundSlash slashScript = slashObj.GetComponent<GroundSlash>();
        Rigidbody rb = slashObj.GetComponent<Rigidbody>();

        // Aplica a velocidade instantânea na direção (forward) que ele acabou de ser rotacionado
        if (slashScript != null && rb != null)
        {
            rb.linearVelocity = slashObj.transform.forward * slashScript.speed;
        }
    }
}