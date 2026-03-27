using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ExoBeasts.Multiplayer.Lobby;
using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Managers;
using System.Collections;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    [Header("Pain�is")]
    public RectTransform painelSelecao;
    public RectTransform painelLobby;

    [Header("Configura��es de Posi��o")]
    public Vector2 posSelecaoCentro = Vector2.zero;
    public Vector2 posSelecaoLado = new Vector2(-400, 0);
    public Vector2 posLobbyEscondido = new Vector2(1200, 0);
    public Vector2 posLobbyVisivel = new Vector2(450, 0);

    [Header("Elementos da UI de Cria��o")]
    public TMP_InputField inputNomeSala;
    public TMP_Text textoMaxJogadores;
    public Toggle togglePublico;

    private int maxPlayersSelecionado = 4;
    private bool lobbyAberto = false;

    private void Awake() => Instance = this;

    void Start()
    {
        // Garante que o painel comece fora da tela
        if (painelLobby != null)
            painelLobby.anchoredPosition = posLobbyEscondido;
        AtualizarTextoPlayers();

        // Em multiplayer, inicia auth EOS e depois abre o painel de lobby
        if (GameModeManager.CurrentMode == GameMode.Multiplayer)
            StartCoroutine(InitMultiplayerFlow());
    }

    /// <summary>
    /// Aguarda EOS inicializar, faz login automatico via Device ID,
    /// e so entao abre o painel de lobby.
    /// </summary>
    private IEnumerator InitMultiplayerFlow()
    {
        // 1. Aguardar EOSManagerWrapper inicializar
        Debug.Log("[LobbyUIManager] Aguardando EOS inicializar...");
        float timeout = 15f;
        float elapsed = 0f;
        while (!EOSManagerWrapper.Instance.IsInitialized && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!EOSManagerWrapper.Instance.IsInitialized)
        {
            Debug.LogError("[LobbyUIManager] EOS nao inicializou a tempo. Verifique o EOSManager na cena.");
            yield break;
        }

        // 2. Fazer login automatico se ainda nao logado
        if (!EOSAuthenticator.Instance.IsLoggedIn)
        {
            Debug.Log("[LobbyUIManager] Iniciando login EOS automatico...");
            bool loginDone = false;
            bool loginOk = false;

            EOSAuthenticator.Instance.OnLoginSuccess += (_) => { loginDone = true; loginOk = true; };
            EOSAuthenticator.Instance.OnLoginFailed += (_) => { loginDone = true; loginOk = false; };
            EOSAuthenticator.Instance.LoginWithDeviceId();

            // Aguardar callback
            elapsed = 0f;
            while (!loginDone && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!loginOk)
            {
                Debug.LogError("[LobbyUIManager] Login EOS falhou. Lobby nao sera aberto.");
                yield break;
            }

            Debug.Log("[LobbyUIManager] Login EOS bem-sucedido!");
        }

        // 3. Abrir painel de lobby
        AbrirPainelMultiplayer();
    }

    // --- NAVEGA��O ---

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

    // --- L�GICA DE CONFIGURA��O (O + e - que voc� pediu) ---

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

    // --- L�GICA DE REDE (Portando do PlaceholderUI) ---

    public void CriarLobbyPelaUI()
    {
        string nome = string.IsNullOrEmpty(inputNomeSala.text) ? "Minha Sala" : inputNomeSala.text;

        // Chama o Singleton do seu LobbyManager real
        LobbyManager.Instance.CreateLobby(new LobbySettings
        {
            lobbyName = nome,
            maxPlayers = maxPlayersSelecionado,
            isPublic = togglePublico.isOn,
            mapName = "CenaMapaTeste"
        });

        Debug.Log($"Criando Lobby: {nome} | Max: {maxPlayersSelecionado}");
    }
}