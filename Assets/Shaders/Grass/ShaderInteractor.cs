using UnityEngine;

public class ShaderInteractor : MonoBehaviour
{
    public float radius = 1f;

    // O Unity chama este método automaticamente para desenhar elementos visuais na janela Scene
    private void OnDrawGizmos()
    {
        // Define a cor da linha do Gizmo para facilitar a visualização (verde neste caso)
        Gizmos.color = Color.yellow;

        // Desenha uma esfera vazada exatamente na posição do objeto, usando o valor de 'radius'
        Gizmos.DrawWireSphere(transform.position, radius);
    }

}