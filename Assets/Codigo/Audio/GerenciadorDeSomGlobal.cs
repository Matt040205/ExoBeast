using UnityEngine;
using UnityEngine.SceneManagement;
using FMODUnity;
using FMOD.Studio;
using FMOD;

public class GerenciadorDeSomGlobal : MonoBehaviour
{
    public static GerenciadorDeSomGlobal Instance;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += AoDescarregarCena;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= AoDescarregarCena;
    }

    private void AoDescarregarCena(Scene cena)
    {
        UnityEngine.Debug.Log($"FMOD: Limpando todos os sons ao sair da cena: {cena.name}");

        ChannelGroup mcg;
        RuntimeManager.CoreSystem.getMasterChannelGroup(out mcg);
        mcg.stop();
    }
}