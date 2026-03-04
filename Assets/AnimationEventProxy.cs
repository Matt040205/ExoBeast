using UnityEngine;

public class AnimationEventProxy : MonoBehaviour
{
    private MeleeCombatSystem meleeSystem;

    // !! A SOLUÇÃO: Variável para guardar qual é a magia que está acontecendo agora !!
    [HideInInspector] public CacadoraNoturnaLogic magiaAtualDaCacadora;

    void Start()
    {
        meleeSystem = GetComponentInParent<MeleeCombatSystem>();
    }

    public void AnimEvent_Hit1()
    {
        if (meleeSystem != null && meleeSystem.enabled) meleeSystem.AnimEvent_Hit1();
    }

    public void AnimEvent_Hit2()
    {
        if (meleeSystem != null && meleeSystem.enabled) meleeSystem.AnimEvent_Hit2();
    }

    public void AnimEvent_Hit3()
    {
        if (meleeSystem != null && meleeSystem.enabled) meleeSystem.AnimEvent_Hit3();
    }

    public void AnimEvent_Hit4()
    {
        if (meleeSystem != null && meleeSystem.enabled) meleeSystem.AnimEvent_Hit4();
    }

    public void AnimEvent_FireBeam()
    {
        // O Proxy agora chama diretamente a magia que se apresentou a ele!
        if (magiaAtualDaCacadora != null && magiaAtualDaCacadora.enabled)
        {
            magiaAtualDaCacadora.AnimEvent_FireBeam();
        }
        else
        {
            Debug.LogWarning("[ERRO] O Animator disparou o evento, mas nenhuma magia foi registrada no Proxy!");
        }
    }
}