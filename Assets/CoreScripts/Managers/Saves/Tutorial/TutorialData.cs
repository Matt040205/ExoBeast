using UnityEngine;

[CreateAssetMenu(fileName = "NovoTutorial", menuName = "ScriptableObjects/Tutorial")]
public class TutorialData : ScriptableObject
{
    [Tooltip("ID único para este tutorial.")]
    public string tutorialID;
    public string titulo;
    [TextArea(3, 10)]
    public string descricao;
}