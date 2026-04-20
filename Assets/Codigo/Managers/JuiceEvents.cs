using System;
using UnityEngine;

[System.Serializable]
public struct CameraShakeConfig
{
    [Tooltip("Quão forte a câmera vai para os lados no impacto.")]
    public float amplitude;
    
    [Tooltip("Quão rápida é a vibração (cortes secos = alta freq).")]
    public float frequency;
    
    [Tooltip("O tempo total em segundos que o cérebro do jogador vai sentir o peso do ataque.")]
    public float duration;

    public CameraShakeConfig(float amp, float freq, float dur)
    {
        amplitude = amp;
        frequency = freq;
        duration = dur;
    }
}

/// <summary>
/// ── JuiceEvents ─────────────────────────────────────────
/// Padrão Observer para os "Juices" e Sensação do Jogo.
/// Atua limitadamente nas máquinas locais.
/// ────────────────────────────────────────────────────────
/// </summary>
public static class JuiceEvents
{
    // Direction, Amplitude, Frequency, Duration
    public static Action<Vector3, float, float, float> OnCameraShake;
}
