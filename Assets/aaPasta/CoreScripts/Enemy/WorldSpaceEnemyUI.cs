using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class WorldSpaceEnemyUI : MonoBehaviour
{
    [Header("Referências")]
    public GameObject damageTextPrefab;

    [Header("Cores do Dano")]
    public Color normalDamageColor = Color.black;
    public Color criticalDamageColor = Color.red;

    [Header("Configurações de Animação")]
    public float animationDuration = 1f;
    public float moveDistance = 1.5f;
    public float fadeOutStartTime = 0.5f;

    [Header("Pooling")]
    public int initialPoolSize = 5;
    private List<TextMeshProUGUI> textPool = new List<TextMeshProUGUI>();

    private Camera mainCamera;

    void Awake()
    {
        InitializePool();
    }

    void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                    mainCamera.transform.rotation * Vector3.up);
        }
    }

    void OnEnable()
    {
        foreach (TextMeshProUGUI textMesh in textPool)
        {
            if (textMesh != null && textMesh.gameObject.activeInHierarchy)
            {
                textMesh.gameObject.SetActive(false);
            }
        }
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                    mainCamera.transform.rotation * Vector3.up);
        }
    }

    private void InitializePool()
    {
        if (damageTextPrefab == null)
        {
            Debug.LogError("Prefab do Texto de Dano não foi definido no WorldSpaceEnemyUI!");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledText();
        }
    }

    private TextMeshProUGUI CreatePooledText()
    {
        GameObject textGO = Instantiate(damageTextPrefab, transform);
        TextMeshProUGUI textMesh = textGO.GetComponent<TextMeshProUGUI>();
        textGO.SetActive(false);
        textPool.Add(textMesh);
        return textMesh;
    }

    private TextMeshProUGUI GetPooledText()
    {
        foreach (var textMesh in textPool)
        {
            if (!textMesh.gameObject.activeInHierarchy)
            {
                return textMesh;
            }
        }
        return CreatePooledText();
    }

    public void ShowDamageNumber(float damageAmount, bool isCritical)
    {
        TextMeshProUGUI textMesh = GetPooledText();
        if (textMesh == null) return;

        textMesh.text = damageAmount.ToString("F0");
        textMesh.color = isCritical ? criticalDamageColor : normalDamageColor;
        textMesh.gameObject.SetActive(true);
        StartCoroutine(AnimateDamageNumber(textMesh));
    }

    private IEnumerator AnimateDamageNumber(TextMeshProUGUI textMesh)
    {
        float elapsedTime = 0f;
        Vector3 startPosition = Vector3.zero;
        Color startColor = textMesh.color;

        textMesh.transform.localPosition = startPosition;
        textMesh.color = new Color(startColor.r, startColor.g, startColor.b, 1f);

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;

            textMesh.transform.localPosition = startPosition + new Vector3(0, progress * moveDistance, 0);

            if (elapsedTime > fadeOutStartTime)
            {
                float fadeProgress = (elapsedTime - fadeOutStartTime) / (animationDuration - fadeOutStartTime);
                textMesh.color = new Color(startColor.r, startColor.g, startColor.b, 1f - fadeProgress);
            }

            yield return null;
        }
        textMesh.gameObject.SetActive(false);
    }
}