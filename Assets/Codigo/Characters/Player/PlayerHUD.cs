using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Referencias de Vida do Jogador")]
    public Image healthBarFill;
    public TMP_Text healthText;
    public Image healthIcon;
    public GameObject regenEffect;

    [Header("Referencias de Vida do Objetivo")]
    public Image objectiveHealthBarFill;
    public TMP_Text objectiveHealthText;
    [Tooltip("A barra de vida que aparece dentro do menu de construcao")]
    public Image BuildModeObjectiveHealthBarFill;
    public TMP_Text BuildModeObjectiveHealthText;

    [Header("Referencias de Municao")]
    public TMP_Text ammoText;
    public Image ammoIcon;
    public GameObject reloadEffect;
    public Slider reloadSlider;

    [Header("Referencias de Moeda")]
    public TMP_Text geoditesText;
    public TMP_Text darkEtherText;

    [Header("Referencias de Habilidades")]
    public Image ability1_Icon;
    public Image ability2_Icon;
    public Image ultimate_Icon;
    public Image ability1_CooldownFill;
    public Image ability2_CooldownFill;
    public Image ultimate_ChargeFill;

    [Header("Configuracoes Visuais")]
    public float healthLerpSpeed = 5f;
    public Color fullHealthColor = Color.green;
    public Color lowHealthColor = Color.red;
    public Color ammoNormalColor = Color.white;
    public Color ammoLowColor = Color.yellow;
    public Color reloadColor = new Color(1f, 0.5f, 0f);

    public static PlayerHUD Instance { get; private set; }

    private PlayerHealthSystem playerHealth;
    private PlayerShooting playerShooting;
    private CommanderAbilityController abilityController;

    private float targetHealthPercent = 1f;
    private bool isRegenerating;
    private bool isSubscribed;

    private float targetObjectiveHealthPercent = 1f;
    private float objectiveCurrentHealth;
    private float objectiveMaxHealth = 1f;
    private bool hasObjectiveHealthState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ObjectiveHealthBus.OnObjectiveHealthChanged += OnObjectiveHealthChanged;

        if (ObjectiveHealthBus.TryGetLastKnown(out float currentHealth, out float maxHealth))
            OnObjectiveHealthChanged(currentHealth, maxHealth);
    }

    public void RegistrarJogador(PlayerHealthSystem health)
    {
        if (health == null || !health.IsOwner)
            return;

        if (playerHealth != null)
            playerHealth.currentHealth.OnValueChanged -= OnPlayerHealthChanged;

        playerHealth = health;
        playerShooting = health.GetComponent<PlayerShooting>();
        abilityController = health.GetComponent<CommanderAbilityController>();

        if (playerHealth != null)
        {
            playerHealth.currentHealth.OnValueChanged += OnPlayerHealthChanged;

            if (abilityController != null && abilityController.characterData != null)
                AtualizarIconesHabilidades(abilityController.characterData);

            OnHealthChanged();
            isSubscribed = true;
        }
    }

    private void Update()
    {
        if (!isSubscribed && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            NetworkObject localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            if (localPlayer != null)
            {
                PlayerHealthSystem health = localPlayer.GetComponent<PlayerHealthSystem>();
                if (health != null)
                    RegistrarJogador(health);
            }
        }

        if (isSubscribed && playerHealth != null && playerHealth.characterData == null)
            OnHealthChanged();

        if (playerHealth != null)
            UpdateHealthDisplay();

        if (playerShooting != null)
            UpdateAmmoDisplay();

        if (hasObjectiveHealthState)
            UpdateObjectiveHealthDisplay();

        if (abilityController != null)
            AtualizarUICooldowns();

        UpdateCurrencyDisplay();
    }

    private void UpdateCurrencyDisplay()
    {
        if (CurrencyManager.Instance == null)
            return;

        if (geoditesText != null)
            geoditesText.text = $"{CurrencyManager.Instance.CurrentGeodites}";

        if (darkEtherText != null)
            darkEtherText.text = $"{CurrencyManager.Instance.CurrentDarkEther}";
    }

    private void OnPlayerHealthChanged(float oldValue, float newValue)
    {
        OnHealthChanged();
    }

    private void OnHealthChanged()
    {
        if (playerHealth == null || playerHealth.characterData == null)
            return;

        targetHealthPercent = playerHealth.currentHealth.Value / playerHealth.characterData.maxHealth;
        isRegenerating = playerHealth.isRegenerating;
    }

    private void UpdateHealthDisplay()
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = Mathf.Lerp(healthBarFill.fillAmount, targetHealthPercent, healthLerpSpeed * Time.deltaTime);

        if (healthText != null && playerHealth.characterData != null)
            healthText.text = $"{Mathf.CeilToInt(playerHealth.currentHealth.Value)}/{playerHealth.characterData.maxHealth}";

        if (regenEffect != null)
            regenEffect.SetActive(isRegenerating);
    }

    private void OnObjectiveHealthChanged(float currentHealth, float maxHealth)
    {
        objectiveCurrentHealth = currentHealth;
        objectiveMaxHealth = Mathf.Max(maxHealth, 1f);
        targetObjectiveHealthPercent = objectiveCurrentHealth / objectiveMaxHealth;
        hasObjectiveHealthState = true;
    }

    private void UpdateObjectiveHealthDisplay()
    {
        if (objectiveHealthBarFill != null)
        {
            objectiveHealthBarFill.fillAmount = Mathf.Lerp(
                objectiveHealthBarFill.fillAmount,
                targetObjectiveHealthPercent,
                healthLerpSpeed * Time.deltaTime);
        }

        if (objectiveHealthText != null)
            objectiveHealthText.text = $"{Mathf.CeilToInt(objectiveCurrentHealth)}/{Mathf.CeilToInt(objectiveMaxHealth)}";

        if (BuildModeObjectiveHealthBarFill != null)
        {
            BuildModeObjectiveHealthBarFill.fillAmount = Mathf.Lerp(
                BuildModeObjectiveHealthBarFill.fillAmount,
                targetObjectiveHealthPercent,
                healthLerpSpeed * Time.deltaTime);
        }

        if (BuildModeObjectiveHealthText != null)
            BuildModeObjectiveHealthText.text = $"{Mathf.CeilToInt(objectiveCurrentHealth)}/{Mathf.CeilToInt(objectiveMaxHealth)}";
    }

    private void UpdateAmmoDisplay()
    {
        if (ammoText != null)
        {
            ammoText.text = $"{playerShooting.currentAmmo} / {playerShooting.maxAmmo}";

            bool isAmmoLow = playerShooting.currentAmmo <= playerShooting.maxAmmo * 0.2f;
            ammoText.color = isAmmoLow ? ammoLowColor : ammoNormalColor;
        }
        else
        {
            Debug.LogWarning("<b>[PlayerHUD AVISO]</b> O campo 'Ammo Text' nao foi preenchido.");
        }

        if (reloadEffect != null)
            reloadEffect.SetActive(playerShooting.isReloading);

        if (reloadSlider == null)
            return;

        reloadSlider.gameObject.SetActive(playerShooting.isReloading);

        if (!playerShooting.isReloading || playerShooting.characterData == null)
            return;

        float reloadProgress = 1f - (playerShooting.GetRemainingReloadTime() / playerShooting.characterData.reloadSpeed);
        reloadSlider.value = reloadProgress;

        if (reloadSlider.fillRect == null)
            return;

        Image sliderFillImage = reloadSlider.fillRect.GetComponent<Image>();
        if (sliderFillImage != null)
            sliderFillImage.color = Color.Lerp(reloadColor, Color.green, reloadProgress);
    }

    private void AtualizarIconesHabilidades(CharacterBase data)
    {
        if (data == null)
            return;

        if (ability1_Icon != null)
        {
            if (data.ability1 != null && data.ability1.icon != null)
            {
                ability1_Icon.sprite = data.ability1.icon;
                ability1_Icon.enabled = true;
            }
            else
            {
                ability1_Icon.enabled = false;
            }
        }

        if (ability2_Icon != null)
        {
            if (data.ability2 != null && data.ability2.icon != null)
            {
                ability2_Icon.sprite = data.ability2.icon;
                ability2_Icon.enabled = true;
            }
            else
            {
                ability2_Icon.enabled = false;
            }
        }

        if (ultimate_Icon != null)
        {
            if (data.ultimate != null && data.ultimate.icon != null)
            {
                ultimate_Icon.sprite = data.ultimate.icon;
                ultimate_Icon.enabled = true;
            }
            else
            {
                ultimate_Icon.enabled = false;
            }
        }
    }

    private void AtualizarUICooldowns()
    {
        if (abilityController.characterData != null)
        {
            if (ability1_CooldownFill != null)
                ability1_CooldownFill.fillAmount =
                    abilityController.GetRemainingCooldownPercent(abilityController.characterData.ability1);

            if (ability2_CooldownFill != null)
                ability2_CooldownFill.fillAmount =
                    abilityController.GetRemainingCooldownPercent(abilityController.characterData.ability2);
        }

        if (ultimate_ChargeFill != null)
            ultimate_ChargeFill.fillAmount = 1f - abilityController.CurrentUltimateCharge;
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.currentHealth.OnValueChanged -= OnPlayerHealthChanged;

        ObjectiveHealthBus.OnObjectiveHealthChanged -= OnObjectiveHealthChanged;
    }
}
