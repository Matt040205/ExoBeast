#if FMOD_PRESENT
using UnityEngine;

public class FmodAninEvent : MonoBehaviour
{
    public void PlayOneShot(string id)
    {
        FmodCommands.Instance.PlayOneShot(id);
    }

    public void PlayLoop(string id)
    {
        FmodCommands.Instance.PlayLoop(id);
    }

    public void Pause(string id)
    {
        FmodCommands.Instance.Pause(id, false);
    }

    public void StopFadeOff(string id)
    {
        FmodCommands.Instance.Stop(id, false);
    }

    public void StopFadeOn(string id)
    {
        FmodCommands.Instance.Stop(id, true);
    }

    public void GetState(string id)
    {
        FmodCommands.Instance.GetState(id);
    }

    public void AddEmitter(FmodEmitterCustom emitterObj)
{
    if (emitterObj != null)
    {
        emitterObj.enabled = true;
        return;
    }

    GameObject emitter = GameObject.FindGameObjectWithTag("FmodEmitter");

    if (emitter == null)
    {
        Debug.Log("Emitter not found");
        return;
    }

    FmodEmitterCustom comp = emitter.GetComponent<FmodEmitterCustom>();

    if (comp != null)
    {
        comp.enabled = true;
    }
}

public void RemoveEmitter(FmodEmitterCustom emitterObj)
{
    if (emitterObj != null)
    {
        emitterObj.enabled = false;
        return;
    }

    GameObject emitter = GameObject.FindGameObjectWithTag("FmodEmitter");

    if (emitter == null)
    {
        Debug.Log("Emitter not found");
        return;
    }

    FmodEmitterCustom comp = emitter.GetComponent<FmodEmitterCustom>();

    if (comp != null)
    {
        comp.enabled = false;
    }
    }
}
#endif
