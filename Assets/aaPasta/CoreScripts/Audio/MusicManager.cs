using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Configuração FMOD")]
    public string eventoMusica = AudioEventIds.MusicDefault;

    private AudioLoopHandle musicLoop;

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
        return musicLoop.IsValid;
    }

    void StartMusic()
    {
        if (!string.IsNullOrEmpty(eventoMusica))
        {
            if (!IsPlaying())
            {
                musicLoop = ExoAudioService.StartLoop(eventoMusica, transform);
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
        ExoAudioService.StopLoop(ref musicLoop);
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
