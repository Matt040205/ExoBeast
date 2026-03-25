using UnityEngine;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Referncias")]
    [Tooltip("Arraste o GameObject 'TutorialPopupPanel' (o que est desativado) aqui.")]
    public GameObject popupPanelObject;
    public List<TutorialData> todosOsTutoriais;

    public Dictionary<string, TutorialData> databaseTutoriais = new Dictionary<string, TutorialData>();
    private TutorialPopupUI popupUIScript;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (TutorialData tutorial in todosOsTutoriais)
        {
            if (tutorial != null && !string.IsNullOrEmpty(tutorial.tutorialID))
            {
                if (!databaseTutoriais.ContainsKey(tutorial.tutorialID))
                {
                    databaseTutoriais.Add(tutorial.tutorialID, tutorial);
                }
                else
                {
                    Debug.LogWarning($"TUTORIAL_MANAGER: ID Duplicado encontrado na lista 'todosOsTutoriais': {tutorial.tutorialID}. Apenas o primeiro ser� usado.");
                }
            }
        }
    }

    public void TriggerTutorial(string tutorialID)
    {
        if (GameDataManager.Instance == null) return;

        if (popupPanelObject == null)
        {
            Debug.LogError("TUTORIAL_MANAGER: 'popupPanelObject' n�o foi anexado no Inspetor!", this.gameObject);
            return;
        }

        if (GameDataManager.Instance.tutoriaisConcluidos.Contains(tutorialID)) return;

        if (databaseTutoriais.ContainsKey(tutorialID))
        {
            TutorialData data = databaseTutoriais[tutorialID];
            if (data == null) return;

            popupPanelObject.SetActive(true);
            popupUIScript = popupPanelObject.GetComponent<TutorialPopupUI>();

            if (popupUIScript != null)
            {
                popupUIScript.Show(data);
            }

            ConcluirTutorial(tutorialID);
        }
        else
        {
            Debug.LogError($"TUTORIAL_MANAGER: O ID [{tutorialID}] n�o foi encontrado no 'databaseTutoriais'.", this.gameObject);
        }
    }

    private void ConcluirTutorial(string tutorialID)
    {
        if (GameDataManager.Instance != null && !GameDataManager.Instance.tutoriaisConcluidos.Contains(tutorialID))
        {
            GameDataManager.Instance.tutoriaisConcluidos.Add(tutorialID);
            GameDataManager.Instance.SaveGame(); // <--- SALVA AQUI
        }
    }
}