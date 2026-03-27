#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

[InitializeOnLoad]
public static class LimpezaDeRedeEditor
{
    static LimpezaDeRedeEditor()
    {
        // Avisa o Unity para rodar essa função toda vez que o botão Play for clicado
        EditorApplication.playModeStateChanged += AoMudarEstadoDoPlay;
    }

    private static void AoMudarEstadoDoPlay(PlayModeStateChange estado)
    {
        // Quando o Unity começar a SAIR do Play Mode (assim que você apertar Stop)
        if (estado == PlayModeStateChange.ExitingPlayMode)
        {
            Debug.Log("<b>[Limpeza Editor]</b> Forçando desligamento da rede para evitar congelamento...");

            // Desliga o Netcode na força bruta
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
    }
}
#endif