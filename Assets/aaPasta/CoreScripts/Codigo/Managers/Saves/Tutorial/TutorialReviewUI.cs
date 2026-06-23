using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// ── TutorialReviewUI ────────────────────────────────────
/// Lista tutoriais ja concluidos para revisao pelo jogador (UI local, sem rede).
///
///  ▸ UpdateList: limpa e reconstroi slots a partir de GameDataManager.tutoriaisConcluidos
///  ▸ Open/Close: controla visibilidade do painel
///  ▸ MonoBehaviour — leitura de dados de save local apenas
/// ─────────────────────────────────────────────────────
/// </summary>
public class TutorialReviewUI : MonoBehaviour
{
    public GameObject slotPrefab;
    public Transform container;
    public TutorialPopupUI detailsPopup;

    public void UpdateList()
    {
        if (GameDataManager.Instance == null) return;

        foreach (Transform child in container) Destroy(child.gameObject);

        foreach (string id in GameDataManager.Instance.tutoriaisConcluidos)
        {
            if (TutorialManager.Instance.databaseTutoriais.ContainsKey(id))
            {
                TutorialData data = TutorialManager.Instance.databaseTutoriais[id];
                GameObject obj = Instantiate(slotPrefab, container);
                // TODO: obj.GetComponent<TutorialSlot>().Setup(data, detailsPopup);
            }
        }
    }

    public void Open()
    {
        gameObject.SetActive(true);
        UpdateList();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
