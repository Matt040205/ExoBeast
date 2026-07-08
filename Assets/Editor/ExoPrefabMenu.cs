using UnityEngine;
using UnityEditor;
using System.IO;

public class ExoPrefabMenu
{
    // O menu "Assets/..." aparece ao clicar com botão direito na pasta Project
    [MenuItem("Assets/Exo Prefabs/Entities/Characters", false, 20)]
    static void CreateCharacter()
    {
        // Pega o objeto selecionado na aba Project
        Object selected = Selection.activeObject;
        string path = AssetDatabase.GetAssetPath(selected);
        Debug.Log("Processando Personagem: " + path);
    }

    [MenuItem("Assets/Exo Prefabs/Entities/Enemies", false, 21)]
    static void CreateEnemies()
    {
        Object selected = Selection.activeObject;
        string path = AssetDatabase.GetAssetPath(selected);
        Debug.Log("Processando Inimigo: " + path);
    }

    // Validação: Só habilita se for um .fbx
    [MenuItem("Assets/Exo Prefabs/Entities/Characters", true)]
    [MenuItem("Assets/Exo Prefabs/Entities/Enemies", true)]
    static bool ValidateFBX()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && Path.GetExtension(path).ToLower() == ".fbx";
    }
}