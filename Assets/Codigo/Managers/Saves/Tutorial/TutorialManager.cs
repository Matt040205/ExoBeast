using UnityEngine;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Referencias")]
    public GameObject popupPanelObject;
    public List<TutorialData> todosOsTutoriais;

    public Dictionary<string, TutorialData> databaseTutoriais = new Dictionary<string, TutorialData>();
    private TutorialPopupUI popupUIScript;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        foreach (TutorialData tutorial in todosOsTutoriais)
        {
            if (tutorial == null || string.IsNullOrEmpty(tutorial.tutorialID)) continue;
            string cleanID = tutorial.tutorialID.Trim();
            tutorial.tutorialID = cleanID;
            if (!databaseTutoriais.ContainsKey(cleanID))
                databaseTutoriais.Add(cleanID, tutorial);
        }

        if (popupPanelObject != null)
        {
            popupUIScript = popupPanelObject.GetComponent<TutorialPopupUI>();
            if (popupUIScript == null)
                popupUIScript = popupPanelObject.GetComponentInChildren<TutorialPopupUI>(true);
            if (popupUIScript == null && popupPanelObject.transform.parent != null)
                popupUIScript = popupPanelObject.transform.parent.GetComponent<TutorialPopupUI>();
            if (popupUIScript == null)
                popupUIScript = popupPanelObject.transform.root.GetComponentInChildren<TutorialPopupUI>(true);
        }
    }

    /// <summary>
    /// Dispara um tutorial. onClose e chamado quando o jogador fechar o popup.
    /// </summary>
    public void TriggerTutorial(string tutorialID, System.Action onClose = null)
    {
        if (GameDataManager.Instance == null || popupUIScript == null) return;

        string cleanID = tutorialID.Trim();
        if (GameDataManager.Instance.tutoriaisConcluidos.Contains(cleanID)) return;

        if (databaseTutoriais.ContainsKey(cleanID))
        {
            TutorialData data = databaseTutoriais[cleanID];
            if (data == null) return;

            popupUIScript.Show(data, onClose);
            ConcluirTutorial(cleanID);
        }
    }

    private void ConcluirTutorial(string tutorialID)
    {
        if (GameDataManager.Instance == null) return;
        if (GameDataManager.Instance.tutoriaisConcluidos.Contains(tutorialID)) return;
        GameDataManager.Instance.tutoriaisConcluidos.Add(tutorialID);
        GameDataManager.Instance.SaveGame();
    }
}
