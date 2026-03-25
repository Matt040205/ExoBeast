using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Configuração FMOD")]
    [EventRef]
    public string eventoMusica = "event:/Music";

    private EventInstance musicInstance;

    void Awake()
    {
        // --- LÓGICA DE SINGLETON ---
        // Garante que só exista UM MusicManager no jogo inteiro.
        // Se tentarmos carregar o Menu de novo, ele destrói o novo e mantém o antigo (que já está tocando).
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Impede que a música seja destruída ao trocar de cena

        StartMusic();
    }

    void StartMusic()
    {
        if (!string.IsNullOrEmpty(eventoMusica))
        {
            musicInstance = RuntimeManager.CreateInstance(eventoMusica);
            musicInstance.start();
            musicInstance.release(); // Libera a memória quando o evento terminar naturalmente (ou for parado)
        }
        else
        {
            Debug.LogWarning("MusicManager: Nenhum evento de música selecionado!");
        }
    }

    // Método opcional caso você queira parar a música via código (ex: créditos finais)
    public void StopMusic()
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }
    }

    // Método opcional para trocar a música dinamicamente
    public void ChangeMusic(string novoEvento)
    {
        StopMusic();
        eventoMusica = novoEvento;
        StartMusic();
    }

    // Garante que o som morra se você fechar o jogo completamente
    private void OnDestroy()
    {
        // Só para o som se ESTA for a instância original que está sendo destruída
        if (Instance == this)
        {
            StopMusic();
        }
    }
}