using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ExoBeasts.Multiplayer.Lobby; // Namespace do seu sistema de lobby
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    [Header("Painéis")]
    public RectTransform painelSelecao;
    public RectTransform painelLobby;

    [Header("Configurações de Posição")]
    public Vector2 posSelecaoCentro = Vector2.zero;
    public Vector2 posSelecaoLado = new Vector2(-400, 0);
    public Vector2 posLobbyEscondido = new Vector2(1200, 0);
    public Vector2 posLobbyVisivel = new Vector2(450, 0);

    [Header("Elementos da UI de Criação")]
    public TMP_InputField inputNomeSala;
    public TMP_Text textoMaxJogadores;
    public Toggle togglePublico;

    private int maxPlayersSelecionado = 4;
    private bool lobbyAberto = false;

    private void Awake() => Instance = this;

    void Start()
    {
        // Garante que o painel comece fora da tela
        painelLobby.anchoredPosition = posLobbyEscondido;
        AtualizarTextoPlayers();
    }

    // --- NAVEGAÇÃO ---

    public void AbrirPainelMultiplayer()
    {
        lobbyAberto = true;
        painelSelecao.DOAnchorPos(posSelecaoLado, 0.5f).SetEase(Ease.OutBack);
        painelLobby.DOAnchorPos(posLobbyVisivel, 0.5f).SetEase(Ease.OutBack);
    }

    public void FecharPainelMultiplayer()
    {
        lobbyAberto = false;
        painelSelecao.DOAnchorPos(posSelecaoCentro, 0.5f).SetEase(Ease.InBack);
        painelLobby.DOAnchorPos(posLobbyEscondido, 0.5f).SetEase(Ease.InBack);
    }

    // --- LÓGICA DE CONFIGURAÇÃO (O + e - que você pediu) ---

    public void AlterarMaxPlayers(int quantidade)
    {
        // Limita entre 2 e 4 jogadores conforme a regra do seu jogo
        maxPlayersSelecionado = Mathf.Clamp(maxPlayersSelecionado + quantidade, 2, 4);
        AtualizarTextoPlayers();
    }

    private void AtualizarTextoPlayers()
    {
        if (textoMaxJogadores != null)
            textoMaxJogadores.text = maxPlayersSelecionado.ToString();
    }

    // --- LÓGICA DE REDE (Portando do PlaceholderUI) ---

    public void CriarLobbyPelaUI()
    {
        string nome = string.IsNullOrEmpty(inputNomeSala.text) ? "Minha Sala" : inputNomeSala.text;

        // Chama o Singleton do seu LobbyManager real
        LobbyManager.Instance.CreateLobby(new LobbySettings
        {
            lobbyName = nome,
            maxPlayers = maxPlayersSelecionado,
            isPublic = togglePublico.isOn,
            mapName = "SceneMapTest" // Nome da cena que vai carregar
        });

        Debug.Log($"Criando Lobby: {nome} | Max: {maxPlayersSelecionado}");
    }
}