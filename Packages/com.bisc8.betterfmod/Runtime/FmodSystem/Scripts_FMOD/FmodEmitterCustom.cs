#if FMOD_PRESENT
using UnityEngine;
using System.Collections;

public class FmodEmitterCustom : MonoBehaviour
{
    public enum EmitterMode
    {
        None,
        Basic,
        Advanced
    }

    public enum PlayEvent
    {
        None,
        OnEnable,
        OnStart,
        OnMouseEnter
    }

    public enum StopEvent
    {
        None,
        OnDisable,
        OnDestroy
    }

    public EmitterMode mode;

    public string eventId;
    public bool is3D = false;
    public bool oneShot = false;

    public PlayEvent playEvent;
    public StopEvent stopEvent;

    public float radius = 5f;
    public Color gizmoColor = Color.cyan;

    private FmodCommands fmod;
    private float appliedRadius = -1f;

    void Awake()
    {
        fmod = FmodCommands.Instance;
    }

    void OnEnable()
    {
        if (playEvent == PlayEvent.OnEnable)
            StartCoroutine(PlayNextFrame());
    }

    IEnumerator PlayNextFrame()
    {
        yield return null;
        Play();
    }

    void Start()
    {
        if (playEvent == PlayEvent.OnStart)
            Play();
    }

    void OnDisable()
    {
        if (stopEvent == StopEvent.OnDisable)
            Stop();
    }

    void OnDestroy()
    {
        if (stopEvent == StopEvent.OnDestroy)
            Stop();
    }

    void OnMouseEnter()
    {
        if (playEvent == PlayEvent.OnMouseEnter)
            Play();
    }

    void Update()
    {
        if (fmod == null || !is3D || oneShot)
            return;

        ApplyRadiusToPlayingEvent();
    }

    public void Play()
    {
        if (fmod == null)
            return;

        if (oneShot)
        {
            if (is3D)
                fmod.PlayOneShot3D(eventId, transform, radius);
            else
                fmod.PlayOneShot(eventId);

            return;
        }

        if (is3D)
            fmod.PlayLoop3D(eventId, transform, radius);
        else
            fmod.PlayLoop(eventId);
    }

    public void Stop(bool fade = true)
    {
        if (fmod == null)
            return;

        fmod.Stop(eventId, fade);
        appliedRadius = -1f;
    }

    public void Pause(bool pause)
    {
        if (fmod == null)
            return;

        fmod.Pause(eventId, pause);
    }

    private void ApplyRadiusToPlayingEvent()
    {
        float validRadius = Mathf.Max(0.01f, radius);

        if (Mathf.Approximately(appliedRadius, validRadius))
            return;

        fmod.Set3DRange(eventId, validRadius);
        appliedRadius = validRadius;
    }

    void OnDrawGizmos()
    {
        if (mode != EmitterMode.Advanced)
            return;

        if (!is3D)
            return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, radius));
    }

    void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
    }
}
#endif
