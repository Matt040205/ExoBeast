using System.Collections.Generic;
using UnityEngine;

public struct AudioLoopHandle
{
    internal string Key;
    internal string EventId;
    internal Transform Target;
    internal float Radius;

    public bool IsValid => !string.IsNullOrWhiteSpace(Key) && !string.IsNullOrWhiteSpace(EventId);
}

public static class ExoAudioService
{
    private const float Default3DRadius = 20f;
    private const float DefaultFadeTime = 0.2f;

    private static readonly HashSet<string> MissingEventWarnings = new HashSet<string>();
    private static int nextLoopId;

#if FMOD_PRESENT
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapBetterFmod()
    {
        EnsureCommands();
    }
#endif

    public static void PlayOneShot(string eventId)
    {
        if (!IsValidEventId(eventId))
            return;

#if FMOD_PRESENT
        FmodCommands commands = EnsureCommands();
        if (commands != null && !commands.TryPlayOneShot(eventId))
            LogMissingEventOnce(eventId);
#else
        LogFmodUnavailableOnce(eventId);
#endif
    }

    public static void PlayOneShot3D(string eventId, Vector3 position)
    {
        if (!IsValidEventId(eventId))
            return;

#if FMOD_PRESENT
        FmodCommands commands = EnsureCommands();
        if (commands != null && !commands.TryPlayOneShot3D(eventId, position, Default3DRadius))
            LogMissingEventOnce(eventId);
#else
        LogFmodUnavailableOnce(eventId);
#endif
    }

    public static AudioLoopHandle StartLoop(string eventId, Transform target)
    {
        AudioLoopHandle handle = CreateLoop(eventId, target);
        StartLoop(ref handle);
        return handle;
    }

    public static AudioLoopHandle CreateLoop(string eventId, Transform target)
    {
        AudioLoopHandle handle = default;

        if (!IsValidEventId(eventId))
            return handle;

#if FMOD_PRESENT
        FmodCommands commands = EnsureCommands();
        if (commands == null || !commands.HasEvent(eventId))
        {
            LogMissingEventOnce(eventId);
            return handle;
        }

        handle.Key = CreateLoopKey(eventId);
        handle.EventId = eventId;
        handle.Target = target;
        handle.Radius = Default3DRadius;
#else
        LogFmodUnavailableOnce(eventId);
#endif

        return handle;
    }

    public static void StartLoop(ref AudioLoopHandle handle)
    {
        if (!handle.IsValid)
            return;

#if FMOD_PRESENT
        FmodCommands commands = EnsureCommands();
        if (commands == null)
            return;

        bool started = handle.Target != null
            ? commands.PlayLoop3DWithKey(handle.EventId, handle.Key, handle.Target, handle.Radius)
            : commands.PlayLoopWithKey(handle.EventId, handle.Key);

        if (!started)
            LogMissingEventOnce(handle.EventId);
#else
        LogFmodUnavailableOnce(handle.EventId);
#endif
    }

    public static void StopLoop(ref AudioLoopHandle handle, bool allowFadeout = true, bool release = true)
    {
        if (!handle.IsValid)
        {
            handle = default;
            return;
        }

#if FMOD_PRESENT
        FmodCommands commands = EnsureCommands();
        if (commands != null)
        {
            if (release)
            {
                commands.Stop(handle.Key, allowFadeout, DefaultFadeTime, true);
            }
            else if (commands.HasInstance(handle.Key))
            {
                commands.Pause(handle.Key, true);
            }
        }
#endif

        if (release)
            handle = default;
    }

    public static void ReleaseLoop(ref AudioLoopHandle handle)
    {
        if (!handle.IsValid)
        {
            handle = default;
            return;
        }

#if FMOD_PRESENT
        FmodCommands commands = EnsureCommands();
        if (commands != null)
            commands.Stop(handle.Key, false, 0f, true);
#endif

        handle = default;
    }

    public static void SetBusVolume(string busPath, float volume)
    {
        if (string.IsNullOrWhiteSpace(busPath))
            return;

#if FMOD_PRESENT
        FmodCommands commands = EnsureCommands();
        if (commands != null && !commands.SetBusVolume(busPath, volume))
            UnityEngine.Debug.LogWarning($"[ExoAudioService] FMOD bus invalido: {busPath}");
#endif
    }

    public static void StopAll()
    {
#if FMOD_PRESENT
        FmodCommands commands = EnsureCommands();
        if (commands != null)
            commands.StopAllAudio();
#endif
    }

    private static bool IsValidEventId(string eventId)
    {
        if (!string.IsNullOrWhiteSpace(eventId))
            return true;

        UnityEngine.Debug.LogWarning("[ExoAudioService] Evento FMOD vazio ignorado.");
        return false;
    }

    private static string CreateLoopKey(string eventId)
    {
        nextLoopId++;
        return $"{eventId}#{nextLoopId}";
    }

    private static void LogMissingEventOnce(string eventId)
    {
        if (MissingEventWarnings.Add(eventId))
            UnityEngine.Debug.LogWarning($"[ExoAudioService] Evento BetterFMOD nao catalogado ou nao carregado: {eventId}");
    }

    private static void LogFmodUnavailableOnce(string eventId)
    {
        if (MissingEventWarnings.Add("FMOD_PRESENT:" + eventId))
            UnityEngine.Debug.LogWarning("[ExoAudioService] FMOD_PRESENT ausente; audio ignorado.");
    }

#if FMOD_PRESENT
    private static FmodCommands EnsureCommands()
    {
        if (FmodCommands.Instance != null)
            return FmodCommands.Instance;

        GameObject audioRoot = new GameObject("[ExoAudio] BetterFMOD");
        Object.DontDestroyOnLoad(audioRoot);
        audioRoot.AddComponent<FmodMultiplayerSettings>();
        return audioRoot.AddComponent<FmodCommands>();
    }
#endif
}
