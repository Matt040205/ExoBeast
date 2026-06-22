using UnityEngine;

public abstract class Ability : ScriptableObject
{
    [Header("Informações da Habilidade")]
    public string abilityName = "Nova Habilidade";
    [TextArea(3, 5)]
    public string description = "Escreva a descrição aqui.";
    public Sprite icon;
    public float cooldown = 1f;

    public virtual void Initialize() { }

    public abstract bool Activate(GameObject quemUsou);
}