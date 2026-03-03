using UnityEngine;

public class DisableIKBehaviour : StateMachineBehaviour
{
    // OnStateEnter é chamado no exato frame em que a animação COMEÇA
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Avisa para desligar o IK
        animator.SetBool("DisableIK", true);
    }

    // OnStateExit é chamado no exato frame em que a animação TERMINA
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Avisa que pode ligar o IK de volta
        animator.SetBool("DisableIK", false);
    }
}