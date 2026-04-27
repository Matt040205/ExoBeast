using UnityEngine;
using Unity.Netcode;

public class CommanderController : MonoBehaviour
{
    [Header("Dados")]
    public CharacterBase characterData;

    [HideInInspector] public float currentHealth;
    [HideInInspector] public int currentAmmo;

    void Start()
    {
        currentHealth = characterData.maxHealth;
        currentAmmo = characterData.magazineSize;

        bool multiplayerActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!multiplayerActive && characterData.passive != null)
            characterData.passive.OnEquip(gameObject);
    }
}
