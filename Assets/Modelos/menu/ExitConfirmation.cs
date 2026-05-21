using UnityEngine;
using System.Collections;

public class ExitConfirmation : MonoBehaviour
{
    [SerializeField] private RectTransform confirmationPanel;
    [SerializeField] private float animationSpeed = 800f;


    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private bool isAnimating = false;

    void Start()
    {
#if UNITY_EDITOR
        if (ExoBeasts.Multiplayer.Core.MppmHelper.IsClone && confirmationPanel == null) { gameObject.SetActive(false); return; }
#endif
        // Configura posi��es
        hiddenPosition = new Vector3(0, -Screen.height / 2 - 200, 0);
        visiblePosition = Vector3.zero;

        // Inicia oculto
        confirmationPanel.anchoredPosition = hiddenPosition;
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        StartCoroutine(AnimatePanel(hiddenPosition, visiblePosition));
    }

    public void Hide()
    {
        StartCoroutine(AnimatePanel(visiblePosition, hiddenPosition, true));
    }

    private IEnumerator AnimatePanel(Vector3 startPos, Vector3 endPos, bool disableAfter = false)
    {
        if (isAnimating) yield break;

        isAnimating = true;
        float timer = 0;

        while (timer < 1f)
        {
            timer += Time.deltaTime * animationSpeed / 100;
            confirmationPanel.anchoredPosition = Vector3.Lerp(startPos, endPos, timer);
            yield return null;
        }

        confirmationPanel.anchoredPosition = endPos;
        isAnimating = false;

        if (disableAfter) gameObject.SetActive(false);
    }

    public void ConfirmExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
