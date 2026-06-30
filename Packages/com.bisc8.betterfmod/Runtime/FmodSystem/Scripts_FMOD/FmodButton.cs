using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FmodButton : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private List<FmodButtonAction> actions = new();

    public void OnPointerClick(PointerEventData eventData)
    {
        Execute(ButtonMoment.OnClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Execute(ButtonMoment.OnEnter);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Execute(ButtonMoment.OnExit);
    }

    private void Execute(ButtonMoment moment)
    {
        foreach (var action in actions)
        {
            if (action.moment != moment)
                continue;

            action.Execute();
        }
    }
}

[Serializable]
public class FmodButtonAction
{
    public ButtonMoment moment = ButtonMoment.None;
    public FmodCommandType command = FmodCommandType.PlayOneShot;

    public string soundId;

    public bool fade;

    public void Execute()
    {
        if (moment == ButtonMoment.None)
            return;

        switch (command)
        {
            case FmodCommandType.PlayOneShot:
                FmodCommands.Instance.PlayOneShot(soundId);
                break;

            case FmodCommandType.PlayLoop:
                FmodCommands.Instance.PlayLoop(soundId);
                break;

            case FmodCommandType.Stop:
                FmodCommands.Instance.Stop(soundId, fade);
                break;

            case FmodCommandType.Pause:
                FmodCommands.Instance.TogglePause(soundId);
                break;
        }
    }
}

public enum ButtonMoment
{
    None,
    OnEnter,
    OnExit,
    OnClick
}

public enum FmodCommandType
{
    PlayOneShot,
    PlayLoop,
    Stop,
    Pause
}