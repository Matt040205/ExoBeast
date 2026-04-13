using UnityEngine;
using System.Collections.Generic;

public class UIPoolManager : MonoBehaviour
{
    public static UIPoolManager Instance { get; private set; }

    [Header("Prefabs")]
    public DamagePopup damagePopupPrefab;
    public MagicStar magicStarPrefab;

    [Header("Configurações do Pool")]
    public int initialPopupCount = 30;
    public int initialStarCount = 60;

    private Queue<DamagePopup> popupPool = new Queue<DamagePopup>();
    private Queue<MagicStar> starPool = new Queue<MagicStar>();
    private Transform poolContainer;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        GameObject containerObj = new GameObject("UI_Pool_Container");
        poolContainer = containerObj.transform;

        InitializePools();
    }

    private void InitializePools()
    {
        for (int i = 0; i < initialPopupCount; i++)
        {
            DamagePopup newPopup = Instantiate(damagePopupPrefab, poolContainer);
            newPopup.gameObject.SetActive(false);
            popupPool.Enqueue(newPopup);
        }

        for (int i = 0; i < initialStarCount; i++)
        {
            MagicStar newStar = Instantiate(magicStarPrefab, poolContainer);
            newStar.gameObject.SetActive(false);
            starPool.Enqueue(newStar);
        }
    }

    public DamagePopup SpawnDamagePopup(Vector3 position, int damageAmount, bool isCritical)
    {
        DamagePopup popup;

        if (popupPool.Count > 0)
        {
            popup = popupPool.Dequeue();
        }
        else
        {
            popup = Instantiate(damagePopupPrefab, poolContainer);
        }

        popup.transform.position = position;
        popup.gameObject.SetActive(true);
        popup.Setup(damageAmount, isCritical);

        return popup;
    }

    public void ReturnPopupToPool(DamagePopup popup)
    {
        popup.gameObject.SetActive(false);
        popupPool.Enqueue(popup);
    }

    public MagicStar SpawnMagicStar(Vector3 position)
    {
        MagicStar star;

        if (starPool.Count > 0)
        {
            star = starPool.Dequeue();
        }
        else
        {
            star = Instantiate(magicStarPrefab, poolContainer);
        }

        star.transform.position = position;
        star.gameObject.SetActive(true);
        star.StartParabolicMovement();

        return star;
    }

    public void ReturnStarToPool(MagicStar star)
    {
        star.gameObject.SetActive(false);
        starPool.Enqueue(star);
    }
}