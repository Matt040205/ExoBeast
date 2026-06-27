using UnityEngine;

public class AnimationEventProxy : MonoBehaviour
{
    private MeleeCombatSystem meleeSystem;

    private MeleeCombatSystem MeleeSystem
    {
        get
        {
            if (meleeSystem == null)
            {
                meleeSystem = GetComponentInParent<MeleeCombatSystem>();
            }
            return meleeSystem;
        }
    }

    // !! A SOLUÇÃO: Variável para guardar qual é a magia que está acontecendo agora !!
    [HideInInspector] public CacadoraNoturnaLogic magiaAtualDaCacadora;

    public void AnimEvent_Hit1()
    {
        var system = MeleeSystem;
        if (system != null && system.enabled) system.AnimEvent_Hit1();
    }

    public void AnimEvent_Hit2()
    {
        var system = MeleeSystem;
        if (system != null && system.enabled) system.AnimEvent_Hit2();
    }

    public void AnimEvent_Hit3()
    {
        var system = MeleeSystem;
        if (system != null && system.enabled) system.AnimEvent_Hit3();
    }

    public void AnimEvent_Hit4()
    {
        var system = MeleeSystem;
        if (system != null && system.enabled) system.AnimEvent_Hit4();
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
