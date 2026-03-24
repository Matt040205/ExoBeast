using UnityEngine;
using System.Collections.Generic;

public class UIPoolManager : MonoBehaviour
{
    // O 'Instance' é como um número de telefone público. 
    // Qualquer prefab (como seus inimigos) pode ligar para cá sem precisar de referência no Inspector.
    public static UIPoolManager Instance { get; private set; }

    [Header("Prefabs")]
    public DamagePopup damagePopupPrefab;
    public MagicStar magicStarPrefab;

    [Header("Configurações do Pool")]
    [Tooltip("Quantos números deixar prontos na memória desde o início.")]
    public int initialPopupCount = 30;
    [Tooltip("Quantas estrelas deixar prontas na memória desde o início.")]
    public int initialStarCount = 60;

    // As "Caixas" onde guardamos os objetos desativados
    private Queue<DamagePopup> popupPool = new Queue<DamagePopup>();
    private Queue<MagicStar> starPool = new Queue<MagicStar>();

    private void Awake()
    {
        // Configura o Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Mais de um UIPoolManager na cena! Destruindo a cópia.");
            Destroy(gameObject);
            return;
        }

        InitializePools();
    }

    private void InitializePools()
    {
        // Fabrica os números e guarda na caixa
        for (int i = 0; i < initialPopupCount; i++)
        {
            DamagePopup newPopup = Instantiate(damagePopupPrefab, transform);
            newPopup.gameObject.SetActive(false);
            popupPool.Enqueue(newPopup);
        }

        // Fabrica as estrelas e guarda na caixa
        for (int i = 0; i < initialStarCount; i++)
        {
            MagicStar newStar = Instantiate(magicStarPrefab, transform);
            newStar.gameObject.SetActive(false);
            starPool.Enqueue(newStar);
        }
    }

    // --- FUNÇÕES DO NÚMERO DE DANO ---

    public DamagePopup SpawnDamagePopup(Vector3 position, int damageAmount, bool isCritical)
    {
        DamagePopup popup;

        // Se tem número sobrando na caixa, pega um. Se não, cria um novo de emergência.
        if (popupPool.Count > 0)
        {
            popup = popupPool.Dequeue();
        }
        else
        {
            popup = Instantiate(damagePopupPrefab, transform);
        }

        // Prepara e ativa o número
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

    // --- FUNÇÕES DA ESTRELA MÁGICA ---

    public MagicStar SpawnMagicStar(Vector3 position)
    {
        MagicStar star;

        if (starPool.Count > 0)
        {
            star = starPool.Dequeue();
        }
        else
        {
            star = Instantiate(magicStarPrefab, transform);
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