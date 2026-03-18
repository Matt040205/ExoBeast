using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class PlayerHUD : MonoBehaviour
{
    [Header("Referências de Vida do Jogador")]
    public Image healthBarFill;
    public TMP_Text healthText;
    public Image healthIcon;
    public GameObject regenEffect;

    [Header("Referências de Vida do Objetivo")]
    public Image objectiveHealthBarFill;
    public TMP_Text objectiveHealthText;
    [Tooltip("A barra de vida que aparece DENTRO do menu de construção (certifique-se que é Image Type: Filled)")]
    public Image BuildModeObjectiveHealthBarFill;
    public TMP_Text BuildModeObjectiveHealthText;

    [Header("Referências de Munição")]
    public TMP_Text ammoText;
    public Image ammoIcon;
    public GameObject reloadEffect;
    public Slider reloadSlider;

    [Header("Referências de Moeda")]
    public TMP_Text geoditesText;
    public TMP_Text darkEtherText;

    [Header("Referências de Habilidades")]
    public Image ability1_Icon;
    public Image ability2_Icon;
    public Image ultimate_Icon;
    public Image ability1_CooldownFill;
    public Image ability2_CooldownFill;
    public Image ultimate_ChargeFill;

    [Header("Configurações Visuais")]
    public float healthLerpSpeed = 5f;
    public Color fullHealthColor = Color.green;
    public Color lowHealthColor = Color.red;
    public Color ammoNormalColor = Color.white;
    public Color ammoLowColor = Color.yellow;
    public Color reloadColor = new Color(1, 0.5f, 0);

    private PlayerHealthSystem playerHealth;
    private PlayerShooting playerShooting;
    private float targetHealthPercent = 1f;
    private bool isRegenerating;

    private ObjectiveHealthSystem objectiveHealth;
    private float targetObjectiveHealthPercent = 1f;

    private CommanderAbilityController abilityController;
    private bool isSubscribed = false;

    void Start()
    {
        FindObjectiveAndShootingSystems();
    }

    void Update()
    {
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerHealth = player.GetComponent<PlayerHealthSystem>();
            }
        }

        if (playerHealth != null && !isSubscribed)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
            OnHealthChanged();
            isSubscribed = true;
        }

        if (abilityController == null && playerHealth != null)
        {
            abilityController = playerHealth.GetComponent<CommanderAbilityController>();
            if (abilityController != null && abilityController.characterData != null)
            {
                AtualizarIconesHabilidades(abilityController.characterData);
            }
        }

        if (playerHealth != null) UpdateHealthDisplay();
        if (playerShooting != null) UpdateAmmoDisplay();
        if (objectiveHealth != null) UpdateObjectiveHealthDisplay();
        if (abilityController != null) AtualizarUICooldowns();
        UpdateCurrencyDisplay();
    }

    private void FindObjectiveAndShootingSystems()
    {
        GameObject objective = GameObject.FindGameObjectWithTag("Objective");
        if (objective != null)
        {
            objectiveHealth = objective.GetComponent<ObjectiveHealthSystem>();
            if (objectiveHealth != null)
            {
                objectiveHealth.OnHealthChanged += OnObjectiveHealthChanged;
                OnObjectiveHealthChanged();
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerShooting = player.GetComponent<PlayerShooting>();
        }
    }

    void UpdateCurrencyDisplay()
    {
        if (CurrencyManager.Instance != null)
        {
            if (geoditesText != null) geoditesText.text = $"{CurrencyManager.Instance.CurrentGeodites}";
            if (darkEtherText != null) darkEtherText.text = $"{CurrencyManager.Instance.CurrentDarkEther}";
        }
    }

    void OnHealthChanged()
    {
        if (playerHealth == null) return;
        targetHealthPercent = playerHealth.currentHealth.Value / playerHealth.characterData.maxHealth;
        isRegenerating = playerHealth.isRegenerating;
    }

    void UpdateHealthDisplay()
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = Mathf.Lerp(healthBarFill.fillAmount, targetHealthPercent, healthLerpSpeed * Time.deltaTime);

        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(playerHealth.currentHealth.Value)}/{playerHealth.characterData.maxHealth}";

        if (regenEffect != null) regenEffect.SetActive(isRegenerating);
    }

    void OnObjectiveHealthChanged()
    {
        if (objectiveHealth == null) return;
        targetObjectiveHealthPercent = objectiveHealth.currentHealth / objectiveHealth.maxHealth;
    }

    void UpdateObjectiveHealthDisplay()
    {
        if (objectiveHealthBarFill != null)
        {
            objectiveHealthBarFill.fillAmount = Mathf.Lerp(objectiveHealthBarFill.fillAmount, targetObjectiveHealthPercent, healthLerpSpeed * Time.deltaTime);
        }
        if (objectiveHealthText != null)
        {
            objectiveHealthText.text = $"{Mathf.CeilToInt(objectiveHealth.currentHealth)}/{objectiveHealth.maxHealth}";
        }

        if (BuildModeObjectiveHealthBarFill != null)
        {
            float novoValor = Mathf.Lerp(BuildModeObjectiveHealthBarFill.fillAmount, targetObjectiveHealthPercent, healthLerpSpeed * Time.deltaTime);
            BuildModeObjectiveHealthBarFill.fillAmount = novoValor;
        }

        if (BuildModeObjectiveHealthText != null)
        {
            BuildModeObjectiveHealthText.text = $"{Mathf.CeilToInt(objectiveHealth.currentHealth)}/{objectiveHealth.maxHealth}";
        }
    }

    void UpdateAmmoDisplay()
    {
        if (playerShooting == null) return;

        if (ammoText != null)
        {
            ammoText.text = $"{playerShooting.currentAmmo}/{playerShooting.characterData.magazineSize}";
            bool isAmmoLow = playerShooting.currentAmmo <= playerShooting.characterData.magazineSize * 0.2f;
            ammoText.color = isAmmoLow ? ammoLowColor : ammoNormalColor;
        }

        if (reloadEffect != null) reloadEffect.SetActive(playerShooting.isReloading);

        if (reloadSlider != null)
        {
            reloadSlider.gameObject.SetActive(playerShooting.isReloading);

            if (playerShooting.isReloading)
            {
                float reloadProgress = 1f - (playerShooting.GetRemainingReloadTime() / playerShooting.characterData.reloadSpeed);
                reloadSlider.value = reloadProgress;

                if (reloadSlider.fillRect != null)
                {
                    Image sliderFillImage = reloadSlider.fillRect.GetComponent<Image>();
                    if (sliderFillImage != null)
                    {
                        sliderFillImage.color = Color.Lerp(reloadColor, Color.green, reloadProgress);
                    }
                }
            }
        }
    }

    void AtualizarIconesHabilidades(CharacterBase data)
    {
        if (data == null) return;

        if (ability1_Icon != null)
        {
            if (data.ability1 != null && data.ability1.icon != null)
            {
                ability1_Icon.sprite = data.ability1.icon;
                ability1_Icon.enabled = true;
            }
            else ability1_Icon.enabled = false;
        }

        if (ability2_Icon != null)
        {
            if (data.ability2 != null && data.ability2.icon != null)
            {
                ability2_Icon.sprite = data.ability2.icon;
                ability2_Icon.enabled = true;
            }
            else ability2_Icon.enabled = false;
        }

        if (ultimate_Icon != null)
        {
            if (data.ultimate != null && data.ultimate.icon != null)
            {
                ultimate_Icon.sprite = data.ultimate.icon;
                ultimate_Icon.enabled = true;
            }
            else ultimate_Icon.enabled = false;
        }
    }

    void AtualizarUICooldowns()
    {
        if (abilityController == null) return;

        if (ability1_CooldownFill != null && abilityController.characterData != null)
            ability1_CooldownFill.fillAmount = abilityController.GetRemainingCooldownPercent(abilityController.characterData.ability1);

        if (ability2_CooldownFill != null && abilityController.characterData != null)
            ability2_CooldownFill.fillAmount = abilityController.GetRemainingCooldownPercent(abilityController.characterData.ability2);

        if (ultimate_ChargeFill != null)
            ultimate_ChargeFill.fillAmount = 1f - abilityController.CurrentUltimateCharge;
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= OnHealthChanged;
        if (objectiveHealth != null) objectiveHealth.OnHealthChanged -= OnObjectiveHealthChanged;
    }
}