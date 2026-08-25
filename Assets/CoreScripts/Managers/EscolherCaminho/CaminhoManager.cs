using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controlador principal da cena EscolherCaminho.
/// Gerencia: ícone do comandante, painel de info do mapa, painel de monstros,
/// aba de detalhes de monstro, painel de modificações (com sorteio) e transição de cena com fade.
/// </summary>
public class CaminhoManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // Ícone do Jogador
    // ─────────────────────────────────────────────────────────
    [Header("Ícone do Jogador")]
    [Tooltip("Image que exibe o ícone do comandante selecionado")]
    public Image iconeJogador;

    [Tooltip("Sprite padrão caso nenhum comandante esteja selecionado")]
    public Sprite spriteIconePadrao;

    // ─────────────────────────────────────────────────────────
    // Painel de Informações do Mapa (PainelInfo)
    // ─────────────────────────────────────────────────────────
    [Header("Painel de Informações do Mapa")]
    [Tooltip("GameObject raiz do PainelInfo (ativado ao clicar num nó)")]
    public GameObject painelInfo;

    [Tooltip("Image que exibe a foto do mapa")]
    public Image fotoMapa;

    [Tooltip("Texto com o nome do mapa")]
    public TextMeshProUGUI tituloMapa;

    [Tooltip("Botão que abre o PainelMonstros a partir do PainelInfo")]
    public Button botaoAbrirMonstros;

    [Tooltip("Botão que abre o PainelModificacoes a partir do PainelInfo")]
    public Button botaoAbrirModificacoes;

    [Tooltip("Botão que confirma a entrada no mapa selecionado")]
    public Button botaoEntrar;

    [Tooltip("Botão que fecha o PainelInfo")]
    public Button botaoCancelar;

    // ─────────────────────────────────────────────────────────
    // Painel de Monstros (PainelMonstros)
    // ─────────────────────────────────────────────────────────
    [Header("Painel de Monstros")]
    [Tooltip("GameObject raiz do PainelMonstros")]
    public GameObject painelMonstros;

    [Tooltip("Texto fixo de título — normalmente apenas 'Monstros'")]
    public TextMeshProUGUI textoTituloMonstros;

    [Tooltip("Transform pai do Grid Layout Group onde os slots de monstro serão instanciados")]
    public Transform gridMonstros;

    [Tooltip("Prefab do slot de monstro (deve ter MonsterSlotUI + Button + Image)")]
    public GameObject prefabSlotMonstro;

    [Tooltip("Botão que fecha o PainelMonstros")]
    public Button botaoFecharMonstros;

    // ─────────────────────────────────────────────────────────
    // Aba de Detalhes do Monstro (AbaDetalhesMonstro)
    // ─────────────────────────────────────────────────────────
    [Header("Aba de Detalhes do Monstro")]
    [Tooltip("GameObject raiz da aba de detalhes (única, compartilhada por todos os monstros)")]
    public GameObject abaDetalhesMonstro;

    [Tooltip("Texto com o nome do monstro")]
    public TextMeshProUGUI textoNomeMonstro;

    [Tooltip("Texto com a descrição do monstro")]
    public TextMeshProUGUI textoDescricaoMonstro;

    [Tooltip("Texto com os detalhes de atributos (vida, dano, velocidade)")]
    public TextMeshProUGUI textoDetalhesMonstro;

    [Tooltip("Botão que fecha a aba de detalhes")]
    public Button botaoFecharDetalhes;

    // ─────────────────────────────────────────────────────────
    // Painel de Modificações (PainelModificacoes)
    // ─────────────────────────────────────────────────────────
    [Header("Painel de Modificações")]
    [Tooltip("GameObject raiz do PainelModificacoes")]
    public GameObject painelModificacoes;

    [Tooltip("Texto fixo de título — normalmente apenas 'Modificações'")]
    public TextMeshProUGUI textoTituloModificacoes;

    [Tooltip("TextMeshPro onde a lista de modificações sorteadas será exibida")]
    public TextMeshProUGUI textoListaModificacoes;

    [Tooltip("Botão que fecha o PainelModificacoes")]
    public Button botaoFecharModificacoes;

    // ─────────────────────────────────────────────────────────
    // Probabilidades do Sorteio
    // ─────────────────────────────────────────────────────────
    [Header("Probabilidades do Sorteio de Modificações")]
    [Tooltip("Chance (%) de nenhuma modificação aparecer")]
    [Range(0, 100)]
    public float chanceNenhuma = 15f;

    [Tooltip("Chance (%) de só a modificação positiva aparecer")]
    [Range(0, 100)]
    public float chanceSoPositiva = 30f;

    [Tooltip("Chance (%) de só a modificação negativa aparecer")]
    [Range(0, 100)]
    public float chanceSoNegativa = 30f;

    [Tooltip("Chance (%) de ambas as modificações aparecerem")]
    [Range(0, 100)]
    public float chanceAmbas = 25f;

    // ─────────────────────────────────────────────────────────
    // Transição de Cena
    // ─────────────────────────────────────────────────────────
    [Header("Transição de Cena")]
    [Tooltip("Duração do fade antes de carregar a cena. Use 0 para transição instantânea.")]
    public float duracaoFade = 0.5f;

    [Tooltip("Image de tela cheia para o efeito de fade (deve estar no topo do Canvas)")]
    public Image imagemFade;

    // ─────────────────────────────────────────────────────────
    // Estado Interno
    // ─────────────────────────────────────────────────────────
    private MapData _mapaAtual;
    private bool _carregandoCena = false;
    private readonly List<GameObject> _slotsInstanciados = new List<GameObject>();

    // Modificações sorteadas para o mapa atual (nulas = não aparece)
    private ModificacaoData _modPositivaSorteada;
    private ModificacaoData _modNegativaSorteada;

    // Nível de progressão: 0 = primeiro par de nós disponível, 1 = segundo, etc.
    // Nós com nivelDoNo > NivelAtualDoCaminho ficam bloqueados.
    public int NivelAtualDoCaminho { get; private set; } = 0;

    // ─────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────
    private void Start()
    {
        DefinirAtivo(painelInfo, false);
        DefinirAtivo(painelMonstros, false);
        DefinirAtivo(abaDetalhesMonstro, false);
        DefinirAtivo(painelModificacoes, false);

        if (imagemFade != null)
        {
            Color c = imagemFade.color;
            c.a = 0f;
            imagemFade.color = c;
            imagemFade.raycastTarget = false;
        }

        RegistrarBotao(botaoEntrar, EntrarNaMapa);
        RegistrarBotao(botaoCancelar, FecharPainel);
        RegistrarBotao(botaoAbrirMonstros, AbrirPainelMonstros);
        RegistrarBotao(botaoFecharMonstros, FecharPainelMonstros);
        RegistrarBotao(botaoFecharDetalhes, FecharAbaDetalhesMonstro);
        RegistrarBotao(botaoAbrirModificacoes, AbrirPainelModificacoes);
        RegistrarBotao(botaoFecharModificacoes, FecharPainelModificacoes);

        CarregarIconeComandante();

        // Recupera o nível atual do caminho salvo
        if (GameDataManager.Instance != null)
        {
            // Sincroniza nível atual
            NivelAtualDoCaminho = GameDataManager.Instance.nivelAtualCaminho;
        }

        NotificarNosDeProgressao();
    }

    private void OnDestroy()
    {
        RemoverBotao(botaoEntrar, EntrarNaMapa);
        RemoverBotao(botaoCancelar, FecharPainel);
        RemoverBotao(botaoAbrirMonstros, AbrirPainelMonstros);
        RemoverBotao(botaoFecharMonstros, FecharPainelMonstros);
        RemoverBotao(botaoFecharDetalhes, FecharAbaDetalhesMonstro);
        RemoverBotao(botaoAbrirModificacoes, AbrirPainelModificacoes);
        RemoverBotao(botaoFecharModificacoes, FecharPainelModificacoes);
    }

    // ─────────────────────────────────────────────────────────
    // Utilitários internos
    // ─────────────────────────────────────────────────────────
    private static void DefinirAtivo(GameObject obj, bool ativo)
    {
        if (obj != null) obj.SetActive(ativo);
    }

    private static void RegistrarBotao(Button btn, UnityEngine.Events.UnityAction acao)
    {
        if (btn != null) btn.onClick.AddListener(acao);
    }

    private static void RemoverBotao(Button btn, UnityEngine.Events.UnityAction acao)
    {
        if (btn != null) btn.onClick.RemoveListener(acao);
    }

    // ─────────────────────────────────────────────────────────
    // Ícone do Comandante
    // ─────────────────────────────────────────────────────────
    private void CarregarIconeComandante()
    {
        if (iconeJogador == null)
        {
            Debug.LogWarning("[CaminhoManager] iconeJogador não está serializado no Inspector!");
            return;
        }

        var gdm = GameDataManager.EnsureInstance();
        gdm.GarantirBibliotecaOriginal();

        if (gdm.equipeSelecionada == null || gdm.equipeSelecionada.Length == 0 || gdm.equipeSelecionada[0] == null)
        {
            gdm.RestaurarSelecao();
        }

        Sprite icone = null;

        if (gdm.equipeSelecionada != null &&
            gdm.equipeSelecionada.Length > 0 &&
            gdm.equipeSelecionada[0] != null)
        {
            var slot0 = gdm.equipeSelecionada[0];
            string nomeBase = slot0.name.Replace("(Clone)", "").Trim();

            // Tenta pegar o ícone do slot
            icone = slot0.characterIcon;

            // Fallback: busca o ScriptableObject original na biblioteca se o clone não tiver ícone
            if (icone == null && gdm.bibliotecaOriginalPersonagens != null)
            {
                var original = gdm.bibliotecaOriginalPersonagens
                    .Find(c => c != null && c.name.Replace("(Clone)", "").Trim() == nomeBase);
                if (original != null)
                    icone = original.characterIcon;
            }

            Debug.Log($"[CaminhoManager] CarregarIconeComandante: slot0='{slot0.name}' (nomeBase='{nomeBase}'), iconeEncontrado={(icone != null ? icone.name : "null")}");
        }
        else
        {
            Debug.LogWarning("[CaminhoManager] GameDataManager ou equipeSelecionada[0] está ausente ao tentar carregar ícone!");
        }

        iconeJogador.sprite = icone != null ? icone : spriteIconePadrao;
        iconeJogador.gameObject.SetActive(iconeJogador.sprite != null);
    }

    // ─────────────────────────────────────────────────────────
    // PainelInfo
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Chamado por PathNodeUI ao clicar num nó. Abre o painel e sorteia as modificações.
    /// </summary>
    public void AbrirPainelMapa(MapData mapa)
    {
        if (mapa == null) return;
        _mapaAtual = mapa;

        // Fecha subpainéis ao trocar de mapa
        DefinirAtivo(painelMonstros, false);
        DefinirAtivo(abaDetalhesMonstro, false);
        DefinirAtivo(painelModificacoes, false);
        LimparGridMonstros();

        // Preenche dados do mapa
        if (tituloMapa != null)
            tituloMapa.text = mapa.mapName;

        if (fotoMapa != null)
        {
            fotoMapa.sprite = mapa.mapPhoto;
            fotoMapa.gameObject.SetActive(mapa.mapPhoto != null);
        }

        // Sorteia as modificações assim que o nó é clicado
        SortearModificacoes(mapa);

        DefinirAtivo(painelInfo, true);
    }

    public void FecharPainel()
    {
        _mapaAtual = null;
        _modPositivaSorteada = null;
        _modNegativaSorteada = null;
        DefinirAtivo(painelInfo, false);
        DefinirAtivo(painelMonstros, false);
        DefinirAtivo(abaDetalhesMonstro, false);
        DefinirAtivo(painelModificacoes, false);
        LimparGridMonstros();
    }

    // ─────────────────────────────────────────────────────────
    // PainelMonstros
    // ─────────────────────────────────────────────────────────
    public void AbrirPainelMonstros()
    {
        if (_mapaAtual == null) return;

        PopularGridMonstros(_mapaAtual.monstros);
        DefinirAtivo(abaDetalhesMonstro, false);
        DefinirAtivo(painelModificacoes, false);
        DefinirAtivo(painelMonstros, true);
    }

    public void FecharPainelMonstros()
    {
        DefinirAtivo(abaDetalhesMonstro, false);
        DefinirAtivo(painelMonstros, false);
        LimparGridMonstros();
    }

    private void PopularGridMonstros(List<EnemyDataSO> monstros)
    {
        LimparGridMonstros();

        if (gridMonstros == null)
        {
            Debug.LogError("[CaminhoManager] gridMonstros não está serializado no Inspector!");
            return;
        }
        if (prefabSlotMonstro == null)
        {
            Debug.LogError("[CaminhoManager] prefabSlotMonstro não está serializado no Inspector!");
            return;
        }
        if (monstros == null || monstros.Count == 0)
        {
            Debug.LogWarning("[CaminhoManager] Nenhum monstro na lista deste mapa!");
            return;
        }

        int criados = 0;
        foreach (var monster in monstros)
        {
            // Ignora entradas nulas na lista de monstros
            if (monster == null) continue;

            GameObject slot = Instantiate(prefabSlotMonstro, gridMonstros);
            var slotUI = slot.GetComponent<MonsterSlotUI>();
            if (slotUI == null)
            {
                slotUI = slot.AddComponent<MonsterSlotUI>();
            }
            slotUI.Configurar(monster, this);
            _slotsInstanciados.Add(slot);
            criados++;
        }

        Debug.Log($"[CaminhoManager] Grid populado com {criados} slot(s) de monstro.");
    }

    private void LimparGridMonstros()
    {
        if (gridMonstros != null)
        {
            // Destrói todos os objetos filhos do grid (inclusive placeholders estáticos da cena)
            for (int i = gridMonstros.childCount - 1; i >= 0; i--)
            {
                var child = gridMonstros.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        _slotsInstanciados.Clear();
    }

    // ─────────────────────────────────────────────────────────
    // AbaDetalhesMonstro
    // ─────────────────────────────────────────────────────────

    /// <summary>Chamado por MonsterSlotUI ao clicar num inimigo do grid.</summary>
    public void AbrirAbaDetalhesMonstro(EnemyDataSO monster)
    {
        if (monster == null) return;

        Debug.Log($"[CaminhoManager] Exibindo detalhes do monstro: {monster.name}");

        if (textoNomeMonstro != null)
            textoNomeMonstro.text = string.IsNullOrEmpty(monster.nomeExibicao)
                ? monster.name  // fallback para o nome do asset
                : monster.nomeExibicao;

        if (textoDescricaoMonstro != null)
            textoDescricaoMonstro.text = string.IsNullOrEmpty(monster.descricao)
                ? "Sem descrição."
                : monster.descricao;

        if (textoDetalhesMonstro != null)
            textoDetalhesMonstro.text =
                $"❤ Vida: {monster.baseHP}\n" +
                $"⚔ Dano: {monster.baseATQ}\n" +
                $"💨 Velocidade: {monster.moveSpeed}";

        if (abaDetalhesMonstro != null)
        {
            abaDetalhesMonstro.transform.SetAsLastSibling();
            DefinirAtivo(abaDetalhesMonstro, true);
        }
    }

    public void FecharAbaDetalhesMonstro()
    {
        DefinirAtivo(abaDetalhesMonstro, false);
    }

    // ─────────────────────────────────────────────────────────
    // Sorteio de Modificações
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sorteia quais modificações aparecerão para este mapa.
    /// Pesos: 15% nenhuma | 30% só positiva | 30% só negativa | 25% ambas
    /// </summary>
    private void SortearModificacoes(MapData mapa)
    {
        _modPositivaSorteada = null;
        _modNegativaSorteada = null;

        // Normaliza os pesos para garantir 100%
        float total = chanceNenhuma + chanceSoPositiva + chanceSoNegativa + chanceAmbas;
        if (total <= 0f) return;

        float roll = Random.Range(0f, total);

        bool sortearPositiva = false;
        bool sortearNegativa = false;

        if (roll < chanceNenhuma)
        {
            // Nenhuma modificação
        }
        else if (roll < chanceNenhuma + chanceSoPositiva)
        {
            sortearPositiva = true;
        }
        else if (roll < chanceNenhuma + chanceSoPositiva + chanceSoNegativa)
        {
            sortearNegativa = true;
        }
        else
        {
            // Ambas
            sortearPositiva = true;
            sortearNegativa = true;
        }

        if (sortearPositiva && mapa.poolPositivas != null && mapa.poolPositivas.Count > 0)
            _modPositivaSorteada = mapa.poolPositivas[Random.Range(0, mapa.poolPositivas.Count)];

        if (sortearNegativa && mapa.poolNegativas != null && mapa.poolNegativas.Count > 0)
            _modNegativaSorteada = mapa.poolNegativas[Random.Range(0, mapa.poolNegativas.Count)];

        Debug.Log($"[CaminhoManager] Sorteio de modificações para '{mapa.mapName}': " +
                  $"Positiva={_modPositivaSorteada?.name ?? "nenhuma"} | " +
                  $"Negativa={_modNegativaSorteada?.name ?? "nenhuma"}");
    }

    // ─────────────────────────────────────────────────────────
    // PainelModificacoes
    // ─────────────────────────────────────────────────────────
    public void AbrirPainelModificacoes()
    {
        DefinirAtivo(painelMonstros, false);
        DefinirAtivo(abaDetalhesMonstro, false);

        // Monta o texto com as modificações sorteadas
        if (textoListaModificacoes != null)
        {
            bool temAlguma = (_modPositivaSorteada != null || _modNegativaSorteada != null);

            if (!temAlguma)
            {
                textoListaModificacoes.text = "Nenhuma modificação nesta fase.";
            }
            else
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                if (_modPositivaSorteada != null)
                    sb.AppendLine($"<color=#44ff88>✦ {_modPositivaSorteada.descricao}</color>");

                if (_modNegativaSorteada != null)
                    sb.AppendLine($"<color=#ff6644>✦ {_modNegativaSorteada.descricao}</color>");

                textoListaModificacoes.text = sb.ToString().TrimEnd();
            }
        }

        DefinirAtivo(painelModificacoes, true);
    }

    public void FecharPainelModificacoes()
    {
        DefinirAtivo(painelModificacoes, false);
    }

    // ─────────────────────────────────────────────────────────
    // Acesso às Modificações Sorteadas (para uso em outros sistemas)
    // ─────────────────────────────────────────────────────────

    /// <summary>Retorna a modificação positiva sorteada para o mapa atual (pode ser null).</summary>
    public ModificacaoData GetModificacaoPositiva() => _modPositivaSorteada;

    /// <summary>Retorna a modificação negativa sorteada para o mapa atual (pode ser null).</summary>
    public ModificacaoData GetModificacaoNegativa() => _modNegativaSorteada;

    // ─────────────────────────────────────────────────────────
    // Transição de Cena
    // ─────────────────────────────────────────────────────────
    public void EntrarNaMapa()
    {
        if (_mapaAtual == null)
        {
            Debug.LogWarning("[CaminhoManager] Nenhum mapa selecionado!");
            return;
        }

        if (_carregandoCena) return;

        if (string.IsNullOrEmpty(_mapaAtual.destinationScene))
        {
            Debug.LogError($"[CaminhoManager] MapData '{_mapaAtual.mapName}' não tem destinationScene definido!");
            return;
        }

        // Avança o nível de progressão
        NivelAtualDoCaminho++;
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.nivelAtualCaminho = NivelAtualDoCaminho;
            GameDataManager.Instance.SaveGame();
        }
        NotificarNosDeProgressao();

        _carregandoCena = true;
        StartCoroutine(CarregarCenaComFade(_mapaAtual.destinationScene));
    }

    /// <summary>
    /// Notifica todos os PathNodeUI da cena para reavaliarem seu estado (Concluido, Disponivel, Futuro).
    /// </summary>
    public void NotificarNosDeProgressao()
    {
        var nos = FindObjectsOfType<PathNodeUI>();
        Debug.Log($"[CaminhoManager] Notificando {nos.Length} nó(s). Nível Atual = {NivelAtualDoCaminho}");
        foreach (var no in nos)
        {
            no.AtualizarEstadoBloqueio();
            Debug.Log($"[CaminhoManager] Nó '{no.gameObject.name}' [nivelDoNo={no.nivelDoNo}] -> Estado: {no.EstadoAtual}");
        }
    }

    /// <summary>
    /// Reseta a progressão do caminho para o início (Nível 0).
    /// Chamado ao iniciar uma nova partida/run.
    /// </summary>
    public void ResetarProgressoCaminho()
    {
        NivelAtualDoCaminho = 0;
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.nivelAtualCaminho = 0;
            GameDataManager.Instance.SaveGame();
        }
        NotificarNosDeProgressao();
    }

    private IEnumerator CarregarCenaComFade(string nomeCena)
    {
        if (imagemFade != null && duracaoFade > 0f)
        {
            imagemFade.raycastTarget = true;
            float tempo = 0f;
            Color c = imagemFade.color;

            while (tempo < duracaoFade)
            {
                tempo += Time.deltaTime;
                c.a = Mathf.Clamp01(tempo / duracaoFade);
                imagemFade.color = c;
                yield return null;
            }

            c.a = 1f;
            imagemFade.color = c;
        }
        else
        {
            yield return null;
        }

        Debug.Log($"[CaminhoManager] Carregando cena: {nomeCena}");
        ModificacaoRunState.SetActive(_modPositivaSorteada, _modNegativaSorteada);

        // Registra o comandante no CharacterChoiceCache ANTES de StartHost
        // para garantir que o GameSetupManager spawne o personagem correto.
        RegistrarComandanteNoCache();

        // Inicia o NetworkManager como host antes de carregar a cena do jogo
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && !nm.IsListening)
        {
            bool started = nm.StartHost();
            if (started)
            {
                nm.SceneManager.LoadScene(nomeCena, UnityEngine.SceneManagement.LoadSceneMode.Single);
                yield break;
            }
            Debug.LogWarning("[CaminhoManager] StartHost() falhou — carregando cena diretamente.");
        }

        SceneManager.LoadScene(nomeCena);
    }

    /// <summary>
    /// Registra o índice do comandante selecionado no CharacterChoiceCache.
    /// Deve ser chamado antes de StartHost para que o GameSetupManager
    /// saiba qual personagem spawnar para o host.
    /// </summary>
    private void RegistrarComandanteNoCache()
    {
        var gdm = GameDataManager.EnsureInstance();
        gdm.GarantirBibliotecaOriginal();

        if (gdm.equipeSelecionada == null || gdm.equipeSelecionada.Length == 0 || gdm.equipeSelecionada[0] == null)
        {
            gdm.RestaurarSelecao();
        }

        var slot0 = gdm.equipeSelecionada != null && gdm.equipeSelecionada.Length > 0
            ? gdm.equipeSelecionada[0]
            : null;

        if (slot0 == null)
        {
            Debug.LogWarning("[CaminhoManager] equipeSelecionada[0] é null — CharacterChoiceCache não registrado.");
            return;
        }

        var biblioteca = gdm.bibliotecaOriginalPersonagens;
        if (biblioteca == null || biblioteca.Count == 0)
        {
            Debug.LogWarning("[CaminhoManager] bibliotecaOriginalPersonagens está vazia — CharacterChoiceCache não registrado.");
            return;
        }

        string nomeBase = slot0.name.Replace("(Clone)", "").Trim();
        int index = biblioteca.FindIndex(c => c != null && c.name.Replace("(Clone)", "").Trim() == nomeBase);

        if (index < 0)
        {
            Debug.LogWarning($"[CaminhoManager] Personagem '{nomeBase}' não encontrado na biblioteca. CharacterChoiceCache não registrado.");
            return;
        }

        ExoBeasts.Multiplayer.Core.CharacterChoiceCache.SetHostCharacterIndex(index, "CaminhoManager");
        Debug.Log($"[CaminhoManager] CharacterChoiceCache registrado com sucesso: index={index} ({nomeBase})");
    }
}
