#if FMOD_PRESENT
using FMOD.Studio;
using FMODUnity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FmodCommands : MonoBehaviour
{
    private const float Default3DRange = 20f;

    public static FmodCommands Instance;

    [SerializeField] private List<CreateFmodList> eventLists;
    [SerializeField] private bool loadResourceEventLists = true;

    private Dictionary<string, EventReference> eventDict =
        new Dictionary<string, EventReference>();

    private Dictionary<string, EventInstance> instances =
        new Dictionary<string, EventInstance>();

    private HashSet<string> missingEventsLogged =
        new HashSet<string>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        RegisterConfiguredEvents();
    }

    private void RegisterConfiguredEvents()
    {
        eventDict.Clear();

        if (eventLists != null)
            foreach (CreateFmodList list in eventLists)
                RegisterList(list);

        if (!loadResourceEventLists)
            return;

        foreach (CreateFmodList list in Resources.LoadAll<CreateFmodList>(string.Empty))
            RegisterList(list);
    }

    public void RegisterList(CreateFmodList list)
    {
        if (list == null || list.type == ListType.None || list.events == null)
            return;

        foreach (FMODListEntry entry in list.events)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                continue;

            if (!eventDict.ContainsKey(entry.id))
                eventDict.Add(entry.id, entry.reference);
        }
    }

    public bool HasEvent(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && eventDict.ContainsKey(id);
    }


    public EventReference GetEvent(string id)
    {
        if (eventDict.TryGetValue(id, out var e))
            return e;

        if (!FmodMultiplayerSettings.MultiplayerModeEnabled)
            LogMissingEvent(id);

        return default;
    }


    public void PlayOneShot(string id)
    {
        TryPlayOneShot(id);
    }

    public bool TryPlayOneShot(string id)
    {
        var reference = GetEvent(id);
        if (reference.IsNull) return false;

        if (!TryCreateInstance(id, reference, out EventInstance instance))
            return false;

        instance.start();
        instance.release();
        return true;
    }

    public void PlayOneShot3D(string id, Transform target, float radius)
    {
        TryPlayOneShot3D(id, target, radius);
    }

    public bool TryPlayOneShot3D(string id, Transform target, float radius)
    {
        if (target == null)
            return TryPlayOneShot(id);

        return TryPlayOneShot3D(id, target.position, radius, target);
    }

    public void PlayOneShot3D(string id, Vector3 position, float radius)
    {
        TryPlayOneShot3D(id, position, radius);
    }

    public bool TryPlayOneShot3D(string id, Vector3 position, float radius)
    {
        return TryPlayOneShot3D(id, position, radius, null);
    }

    private bool TryPlayOneShot3D(string id, Vector3 position, float radius, Transform target)
    {
        var reference = GetEvent(id);
        if (reference.IsNull) return false;

        if (!TryCreateInstance(id, reference, out EventInstance instance))
            return false;

        if (target != null)
            RuntimeManager.AttachInstanceToGameObject(instance, target.gameObject);
        else
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));

        Apply3DRange(instance, radius);

        instance.start();
        instance.release();
        return true;
    }


    public void PlayLoop(string id, bool fade = false, float fadeTime = 1f)
    {
        PlayLoopWithKey(id, id, fade, fadeTime);
    }

    public bool PlayLoopWithKey(string id, string instanceKey, bool fade = false, float fadeTime = 1f)
    {
        return TryStartLoop(id, instanceKey, null, Vector3.zero, Default3DRange, false, fade, fadeTime);
    }


    public void PlayLoop3D(string id, Transform target, float radius, bool fade = false, float fadeTime = 1f)
    {
        PlayLoop3DWithKey(id, id, target, radius, fade, fadeTime);
    }

    public bool PlayLoop3DWithKey(string id, string instanceKey, Transform target, float radius, bool fade = false, float fadeTime = 1f)
    {
        if (target == null)
            return PlayLoopWithKey(id, instanceKey, fade, fadeTime);

        return TryStartLoop(id, instanceKey, target, target.position, radius, true, fade, fadeTime);
    }

    private bool TryStartLoop(string id, string instanceKey, Transform target, Vector3 position, float radius, bool is3D, bool fade, float fadeTime)
    {
        if (string.IsNullOrWhiteSpace(instanceKey))
            instanceKey = id;

        if (instances.TryGetValue(instanceKey, out EventInstance existingInstance))
        {
            if (!existingInstance.isValid())
            {
                instances.Remove(instanceKey);
            }
            else
            {
                existingInstance.setPaused(false);
                existingInstance.setVolume(1);
                return true;
            }
        }

        var reference = GetEvent(id);
        if (reference.IsNull) return false;

        if (!TryCreateInstance(id, reference, out EventInstance instance))
            return false;

        if (is3D)
        {
            if (target != null)
                RuntimeManager.AttachInstanceToGameObject(instance, target.gameObject);
            else
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));

            Apply3DRange(instance, radius);
        }

        if (fade)
            instance.setVolume(0);

        instance.start();

        instances[instanceKey] = instance;

        if (fade)
            StartCoroutine(FadeIn(instance, fadeTime));

        return true;
    }

    public bool HasInstance(string id)
    {
        return instances.TryGetValue(id, out EventInstance instance) && instance.isValid();
    }

    public void Set3DRange(string id, float radius)
    {
        if (!instances.TryGetValue(id, out var instance))
            return;

        Apply3DRange(instance, radius);
    }

    private void Apply3DRange(EventInstance instance, float radius)
    {
        float maxDistance = Mathf.Max(0.01f, radius);

        instance.setProperty(EVENT_PROPERTY.MINIMUM_DISTANCE, 0f);
        instance.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, maxDistance);
    }

    private bool TryCreateInstance(string id, EventReference reference, out EventInstance instance)
    {
        instance = default;

        try
        {
            instance = RuntimeManager.CreateInstance(reference);
            return true;
        }
        catch (EventNotFoundException)
        {
            LogMissingEvent(id);
            return false;
        }
    }

    private void LogMissingEvent(string id)
    {
        if (FmodMultiplayerSettings.MultiplayerModeEnabled)
            return;

        if (missingEventsLogged.Add(id))
            Debug.LogWarning("[FMOD] Event not found: " + id);
    }


    public void Stop(string id, bool fade = false, float fadeTime = 1f)
    {
        Stop(id, fade, fadeTime, true);
    }

    public void Stop(string id, bool fade, float fadeTime, bool release)
    {
        if (!instances.TryGetValue(id, out var instance))
            return;

        if (fade)
        {
            StartCoroutine(FadeOutAndStop(id, instance, fadeTime, release));
            return;
        }

        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);

        if (release)
        {
            instance.release();
            instances.Remove(id);
        }
    }

    IEnumerator FadeOutAndStop(string id, EventInstance instance, float fadeTime, bool release)
    {
        instance.getVolume(out float start);

        float t = 0;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            instance.setVolume(Mathf.Lerp(start, 0, t / fadeTime));
            yield return null;
        }

        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);

        if (release)
        {
            instance.release();
            instances.Remove(id);
        }
        else
        {
            instance.setVolume(1);
        }
    }

    IEnumerator FadeIn(EventInstance instance, float fadeTime)
    {
        float t = 0;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            instance.setVolume(Mathf.Lerp(0, 1, t / fadeTime));
            yield return null;
        }

        instance.setVolume(1);
    }


    public PLAYBACK_STATE GetState(string id)
    {
        if (instances.TryGetValue(id, out var instance))
        {
            instance.getPlaybackState(out var state);
            return state;
        }

        return PLAYBACK_STATE.STOPPED;
    }


    public void Pause(string id, bool pause)
    {
        if (instances.TryGetValue(id, out var instance))
            instance.setPaused(pause);
    }

    public void TogglePause(string id)
    {
        if (instances.TryGetValue(id, out var instance))
        {
            instance.getPaused(out bool paused);
            instance.setPaused(!paused);
        }
    }

    public bool SetBusVolume(string busPath, float volume)
    {
        if (string.IsNullOrWhiteSpace(busPath))
            return false;

        try
        {
            Bus bus = RuntimeManager.GetBus(busPath);
            if (!bus.isValid())
                return false;

            bus.setVolume(Mathf.Clamp01(volume));
            return true;
        }
        catch (BusNotFoundException)
        {
            Debug.LogWarning("[FMOD] Bus not found: " + busPath);
            return false;
        }
    }

    public void StopAllAudio()
    {
        foreach (EventInstance instance in instances.Values)
        {
            if (!instance.isValid())
                continue;

            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
        }

        instances.Clear();
        RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup masterChannelGroup);
        masterChannelGroup.stop();
    }
}
#endif
