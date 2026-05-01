using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

[System.Serializable]
public class CharacterSaveData
{
    public string characterName;
    public List<string> unlockedSkills = new List<string>();
    public int pointsSpent;
    public int pointsAvailable;

    public float maxHealth;
    public float damage;
    public float moveSpeed;
    public float attackSpeed;
    public float armor;
    public float critChance;
    public float critDamage;
    public float armorPenetration;
}

[System.Serializable]
public class FullSaveData
{
    public List<string> tutorials = new List<string>();
    public List<CharacterSaveData> characters = new List<CharacterSaveData>();
    public string[] teamSelection = new string[8]; // nome do CharacterBase em cada slot ("" = vazio)
}

/// <summary>
/// ── GameDataManager ─────────────────────────────────────
/// Gerencia o banco de dados de personagens e o sistema de Save/Load.
/// 
///  ▸ Persistência via JSON no PersistentDataPath.
///  ▸ Cache de dados para aplicação em instâncias de CharacterBase.
///  ▸ Singleton persistente entre cenas.
/// ───────────────────────────────────────────────────────
/// </summary>
public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance;
    private static List<CharacterBase> _bootstrapLibrary;

    [Header("Banco de Dados")]
    public List<CharacterBase> bibliotecaOriginalPersonagens;

    [Header("Sessão do Jogador (Runtime)")]
    public List<CharacterBase> personagensDoJogador = new List<CharacterBase>();

    [Header("Estado Atual")]
    public CharacterBase[] equipeSelecionada = new CharacterBase[8];
    public CharacterBase personagemParaRastros;

    [Header("Progresso dos Tutoriais")]
    public List<string> tutoriaisConcluidos = new List<string>();

    [Header("Sessão Multiplayer")]
    public int totalDeJogadores = 1;

    private Dictionary<string, CharacterSaveData> loadedCharacterData = new Dictionary<string, CharacterSaveData>();
    private string[] _savedTeamSelection;
    private string saveFilePath;

    public static GameDataManager EnsureInstance(IEnumerable<CharacterBase> bootstrapLibrary = null)
    {
        QueueBootstrapLibrary(bootstrapLibrary);

        if (Instance == null)
        {
            GameObject go = new GameObject("GameDataManager");
            return go.AddComponent<GameDataManager>();
        }

        Instance.ApplyBootstrapLibraryIfNeeded(rebuildRuntimeCharactersIfNeeded: true);
        return Instance;
    }

    public static void QueueBootstrapLibrary(IEnumerable<CharacterBase> bootstrapLibrary)
    {
        if (bootstrapLibrary == null)
            return;

        _bootstrapLibrary = bootstrapLibrary
            .Where(character => character != null)
            .ToList();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            saveFilePath = Path.Combine(Application.persistentDataPath, "savegame.json");
            ApplyBootstrapLibraryIfNeeded(rebuildRuntimeCharactersIfNeeded: false);
            RebuildRuntimeCharacterRoster();
            LoadGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LimparSelecao()
    {
        for (int i = 0; i < equipeSelecionada.Length; i++)
        {
            // Apenas remove a referência daquele "slot", sem destruir a master-copy
            // original que está guardada no personagensDoJogador! 
            equipeSelecionada[i] = null;
        }
    }

    public void SaveGame()
    {
        FullSaveData data = new FullSaveData();
        data.tutorials = new List<string>(tutoriaisConcluidos);

        foreach (var kvp in loadedCharacterData)
        {
            data.characters.Add(kvp.Value);
        }

        foreach (CharacterBase charInstance in equipeSelecionada)
        {
            if (charInstance != null)
            {
                string cleanName = charInstance.name.Replace("(Clone)", "");
                data.characters.RemoveAll(x => x.characterName == cleanName);

                CharacterSaveData charData = new CharacterSaveData();
                charData.characterName = cleanName;
                charData.unlockedSkills = new List<string>(charInstance.habilidadesDesbloqueadas);
                charData.pointsSpent = charInstance.pontosRastrosGastos;
                charData.pointsAvailable = charInstance.pontosRastrosDisponiveis;
                charData.maxHealth = charInstance.maxHealth;
                charData.damage = charInstance.damage;
                charData.moveSpeed = charInstance.moveSpeed;
                charData.attackSpeed = charInstance.attackSpeed;
                charData.armor = charInstance.armor;
                charData.critChance = charInstance.critChance;
                charData.critDamage = charInstance.critDamage;
                charData.armorPenetration = charInstance.armorPenetration;

                data.characters.Add(charData);
                loadedCharacterData[cleanName] = charData;
            }
        }

        data.teamSelection = new string[equipeSelecionada.Length];
        for (int i = 0; i < equipeSelecionada.Length; i++)
        {
            if (equipeSelecionada[i] != null)
            {
                data.teamSelection[i] = equipeSelecionada[i].name.Replace("(Clone)", "");
            }
            else
            {
                data.teamSelection[i] = "";
            }
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log("JOGO SALVO em: " + saveFilePath);
    }

    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            try
            {
                string json = File.ReadAllText(saveFilePath);
                FullSaveData data = JsonUtility.FromJson<FullSaveData>(json);
                tutoriaisConcluidos = data.tutorials;
                loadedCharacterData.Clear();
                foreach (var charData in data.characters)
                {
                    if (!loadedCharacterData.ContainsKey(charData.characterName))
                        loadedCharacterData.Add(charData.characterName, charData);
                }
                if (data.teamSelection != null && data.teamSelection.Length > 0)
                    _savedTeamSelection = data.teamSelection;
                
                // Distribui o profile para os personagens master atuais em Play Mode
                foreach (var personagem in personagensDoJogador)
                {
                    AplicarDadosCarregados(personagem);
                }

                Debug.Log("JOGO CARREGADO.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Erro ao carregar save: " + e.Message);
            }
        }
    }

    public void RestaurarSelecao()
    {
        if (_savedTeamSelection == null || bibliotecaOriginalPersonagens == null) return;

        int count = Mathf.Min(_savedTeamSelection.Length, equipeSelecionada.Length);
        for (int i = 0; i < count; i++)
        {
            if (string.IsNullOrEmpty(_savedTeamSelection[i])) continue;
            if (equipeSelecionada[i] != null) continue; // respeita seleção já feita na sessão

            CharacterBase found = personagensDoJogador.Find(c => c.name.Replace("(Clone)", "") == _savedTeamSelection[i]);
            if (found != null)
            {
                equipeSelecionada[i] = found; // Reaproveita a cópia mestra sem recriar
            }
        }
    }

    public void AplicarDadosCarregados(CharacterBase instanciaPersonagem)
    {
        string cleanName = instanciaPersonagem.name.Replace("(Clone)", "");
        if (loadedCharacterData.ContainsKey(cleanName))
        {
            CharacterSaveData data = loadedCharacterData[cleanName];
            instanciaPersonagem.pontosRastrosGastos = data.pointsSpent;
            instanciaPersonagem.pontosRastrosDisponiveis = data.pointsAvailable;
            instanciaPersonagem.habilidadesDesbloqueadas = new List<string>(data.unlockedSkills);
            instanciaPersonagem.maxHealth = data.maxHealth;
            instanciaPersonagem.damage = data.damage;
            instanciaPersonagem.moveSpeed = data.moveSpeed;
            instanciaPersonagem.attackSpeed = data.attackSpeed;
            instanciaPersonagem.armor = data.armor;
            instanciaPersonagem.critChance = data.critChance;
            instanciaPersonagem.critDamage = data.critDamage;
            instanciaPersonagem.armorPenetration = data.armorPenetration;
        }
    }

    [ContextMenu("Apagar Save")]
    public void DeleteSave()
    {
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
            tutoriaisConcluidos.Clear();
            loadedCharacterData.Clear();
            Debug.Log("Save apagado!");
        }
    }

    private void ApplyBootstrapLibraryIfNeeded(bool rebuildRuntimeCharactersIfNeeded)
    {
        if (_bootstrapLibrary != null && _bootstrapLibrary.Count > 0 &&
            (bibliotecaOriginalPersonagens == null || bibliotecaOriginalPersonagens.Count == 0))
        {
            bibliotecaOriginalPersonagens = new List<CharacterBase>(_bootstrapLibrary);
        }

        if (bibliotecaOriginalPersonagens == null)
            bibliotecaOriginalPersonagens = new List<CharacterBase>();

        _bootstrapLibrary = null;

        if (!rebuildRuntimeCharactersIfNeeded)
            return;

        if (bibliotecaOriginalPersonagens.Count == 0 || personagensDoJogador.Count > 0)
            return;

        RebuildRuntimeCharacterRoster();
        LoadGame();
    }

    private void RebuildRuntimeCharacterRoster()
    {
        personagensDoJogador.Clear();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var original in bibliotecaOriginalPersonagens)
        {
            if (original == null) continue;
            if (!seen.Add(original.name))
            {
                Debug.LogWarning($"[GameDataManager] Personagem duplicado ignorado: {original.name}. Remova a entrada extra de 'bibliotecaOriginalPersonagens' no Inspector.");
                continue;
            }

            CharacterBase clone = Instantiate(original);
            clone.habilidadesDesbloqueadas = new List<string>();
            clone.pontosPorCaminho = new List<CaminhoRastrosData>();
            clone.pontosRastrosGastos = 0;
            personagensDoJogador.Add(clone);
        }
    }
}
