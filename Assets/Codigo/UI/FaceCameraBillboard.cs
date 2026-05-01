using UnityEngine;

public class FaceCameraBillboard : MonoBehaviour
{
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        // Se a câmera sumir ou for destruída, tenta achar de novo
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return;
        }

        // Isso garante que o objeto sempre aponte na mesma direção que a câmera está olhando.
        // Como o jogo é um TPS com câmera livre, isso faz a imagem 2D ficar sempre chapada para a tela
        // independente de como o corpo do monstro ou o eixo do indicador rotacionar.
        transform.forward = mainCam.transform.forward;
    }
}
