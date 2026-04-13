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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Verifica se a música já está tocando para não sobrepor
        if (!IsPlaying())
        {
            StartMusic();
        }
    }

    public bool IsPlaying()
    {
        if (musicInstance.isValid())
        {
            PLAYBACK_STATE state;
            musicInstance.getPlaybackState(out state);
            return state != PLAYBACK_STATE.STOPPED;
        }
        return false;
    }

    void StartMusic()
    {
        if (!string.IsNullOrEmpty(eventoMusica))
        {
            // Se já existir uma instância válida, vamos apenas garantir que ela toque
            if (!musicInstance.isValid())
            {
                musicInstance = RuntimeManager.CreateInstance(eventoMusica);
            }

            if (!IsPlaying())
            {
                musicInstance.start();
            }
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
            musicInstance.release(); // Agora liberamos apenas quando paramos de verdade
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
