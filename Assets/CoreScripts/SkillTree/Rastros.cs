using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class Rastros : MonoBehaviour
{
    [Header("Referências da UI (Árvore)")]
    public List<BotaoHabilidade> todosBotoesDaArvore;
    public TextMeshProUGUI textoPontosDisponiveis;

    [Header("Painel de Detalhes da Habilidade")]
    public TextMeshProUGUI textoNomePersonagem;
    public TextMeshProUGUI textoDescricaoHabilidade;
    public Button botaoConfirmarCompra;
    public GameObject painelDescricao;

    [Header("Mapeamento de Ícones")]
    [Tooltip("Arraste o ScriptableObject 'Banco de Icones de Status' aqui.")]
    public StatIconDatabase iconDatabase;

    [Header("Debug")]
    [Tooltip("Personagem para testar a cena sem vir da Seleção")]
    public CharacterBase personagemParaDebug;

    private CharacterBase personagemSendoUprado;
    private BotaoHabilidade habilidadeSelecionada;

    public int pontosDisponiveis => personagemSendoUprado?.pontosRastrosDisponiveis ?? 0;
    public int pontosGastosGlobal => personagemSendoUprado?.pontosRastrosGastos ?? 0;
    public List<CaminhoRastrosData> pontosPorCaminho => personagemSendoUprado?.pontosPorCaminho;
    public List<string> habilidadesDesbloqueadas => personagemSendoUprado?.habilidadesDesbloqueadas;


    void Awake()
    {
        if (GameDataManager.Instance != null && GameDataManager.Instance.personagemParaRastros != null)
        {
            personagemSendoUprado = GameDataManager.Instance.personagemParaRastros;
        }
        else if (personagemParaDebug != null)
        {
            Debug.LogWarning("Rastros: GameDataManager vazio. Usando personagem de DEBUG.");
            personagemSendoUprado = personagemParaDebug;
        }
        else
        {
            Debug.LogError("Personagem para Rastros não encontrado E personagem de DEBUG não definido! A cena não pode funcionar.");
            if (textoNomePersonagem != null) textoNomePersonagem.text = "ERRO";
            if (textoPontosDisponiveis != null) textoPontosDisponiveis.text = "Pontos: -";
            return;
        }

        if (textoNomePersonagem != null)
        {
            textoNomePersonagem.text = personagemSendoUprado.name.Replace("(Clone)", "");
        }

        InicializarCaminhos();
        AtualizarIconesBotoes();

        if (painelDescricao != null)
        {
            painelDescricao.SetActive(false);
        }
        if (botaoConfirmarCompra != null)
        {
            botaoConfirmarCompra.onClick.RemoveAllListeners();
            botaoConfirmarCompra.onClick.AddListener(ConfirmarCompraHabilidade);
        }

        AtualizarUI();
    }

    void Start()
    {
        if (personagemSendoUprado == null) return;

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.TriggerTutorial("EXPLAIN_RASTROS");
        }

        AtualizarEstadoBotoes();
    }

    private int GetPontosNoCaminho(string idCaminho)
    {
        if (pontosPorCaminho == null) return 0;
        foreach (var caminho in pontosPorCaminho)
        {
            if (caminho.idCaminho == idCaminho)
            {
                return caminho.pontosGastos;
            }
        }
        return 0;
    }

    private void AddPontoNoCaminho(string idCaminho)
    {
        if (pontosPorCaminho == null) return;
        for (int i = 0; i < pontosPorCaminho.Count; i++)
        {
            if (pontosPorCaminho[i].idCaminho == idCaminho)
            {
                var data = pontosPorCaminho[i];
                data.pontosGastos++;
                pontosPorCaminho[i] = data;
                return;
            }
        }
        pontosPorCaminho.Add(new CaminhoRastrosData { idCaminho = idCaminho, pontosGastos = 1 });
    }

    void InicializarCaminhos()
    {
        if (personagemSendoUprado != null && personagemSendoUprado.pontosPorCaminho == null)
        {
            personagemSendoUprado.pontosPorCaminho = new List<CaminhoRastrosData>();
        }
        if (personagemSendoUprado != null && personagemSendoUprado.habilidadesDesbloqueadas == null)
        {
            personagemSendoUprado.habilidadesDesbloqueadas = new List<string>();
        }
    }

    void AtualizarIconesBotoes()
    {
        if (iconDatabase == null)
        {
            Debug.LogWarning("Nenhum Banco de Ícones (StatIconDatabase) foi assignado no script Rastros.");
            return;
        }

        foreach (var botao in todosBotoesDaArvore)
        {
            if (botao == null || botao.iconeHabilidade == null) continue;
            RastroUpgrade upgrade = botao.GetUpgradeParaPersonagem(personagemSendoUprado);
            if (upgrade != null && upgrade.modifiers.Count > 0)
            {
                CharacterStatType statPrincipal = upgrade.modifiers[0].statToModify;
                Sprite icon = iconDatabase.GetIconForStat(statPrincipal);
                if (icon != null)
                {
                    botao.iconeHabilidade.sprite = icon;
                }
                else
                {
                    Debug.LogWarning($"Ícone para o status {statPrincipal} não encontrado (Botão: {botao.idHabilidade}).");
                }
            }
            else
            {
                botao.iconeHabilidade.enabled = false;
            }
        }
    }

    public void SelecionarHabilidade(BotaoHabilidade botao)
    {
        habilidadeSelecionada = botao;

        if (habilidadeSelecionada == null || personagemSendoUprado.habilidadesDesbloqueadas.Contains(habilidadeSelecionada.idHabilidade))
        {
            if (painelDescricao != null) painelDescricao.SetActive(false);
            habilidadeSelecionada = null;
            return;
        }

        if (painelDescricao != null) painelDescricao.SetActive(true);

        RastroUpgrade upgradeCorreto = habilidadeSelecionada.GetUpgradeParaPersonagem(personagemSendoUprado);

        if (upgradeCorreto != null)
        {
            textoDescricaoHabilidade.text = $"<b>{upgradeCorreto.upgradeName}</b>\n\n{upgradeCorreto.description}";
        }
        else
        {
            textoDescricaoHabilidade.text = $"Habilidade não configurada para {personagemSendoUprado.name}.";
        }

        bool podeComprar = ChecarCondicoes(habilidadeSelecionada);
        botaoConfirmarCompra.interactable = podeComprar && (upgradeCorreto != null);
    }

    public void ConfirmarCompraHabilidade()
    {
        if (habilidadeSelecionada == null) return;
        bool sucesso = TentarDesbloquear(habilidadeSelecionada);
        if (painelDescricao != null) painelDescricao.SetActive(false);
        habilidadeSelecionada = null;
    }

    private bool ChecarCondicoes(BotaoHabilidade botao)
    {
        if (botao == null || personagemSendoUprado == null) return false;
        if (personagemSendoUprado.habilidadesDesbloqueadas.Contains(botao.idHabilidade)) return false;
        if (personagemSendoUprado.pontosRastrosDisponiveis < botao.custo) return false;
        if (!string.IsNullOrEmpty(botao.preRequisitoHabilidade) && !personagemSendoUprado.habilidadesDesbloqueadas.Contains(botao.preRequisitoHabilidade)) return false;
        if (personagemSendoUprado.pontosRastrosGastos < botao.pontosGlobaisNecessarios) return false;

        int pontosAtuaisNesteCaminho = GetPontosNoCaminho(botao.idCaminho);
        if (pontosAtuaisNesteCaminho < botao.pontosNoCaminhoNecessarios) return false;

        return true;
    }

    private bool TentarDesbloquear(BotaoHabilidade botao)
    {
        if (!ChecarCondicoes(botao))
        {
            return false;
        }

        RastroUpgrade upgradeCorreto = botao.GetUpgradeParaPersonagem(personagemSendoUprado);

        if (upgradeCorreto == null)
        {
            Debug.LogError($"Tentativa de comprar {botao.idHabilidade} falhou: Upgrade não mapeado para {personagemSendoUprado.name}!");
            return false;
        }

        personagemSendoUprado.pontosRastrosDisponiveis -= botao.custo;
        personagemSendoUprado.pontosRastrosGastos += 1;
        AddPontoNoCaminho(botao.idCaminho);
        personagemSendoUprado.habilidadesDesbloqueadas.Add(botao.idHabilidade);

        AplicarUpgrade(upgradeCorreto);

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.TriggerTutorial("RETURN_TO_SELECTION");
        }

        // --- SALVA O JOGO APÓS COMPRAR ---
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SaveGame();
        }

        AtualizarUI();
        AtualizarEstadoBotoes();
        return true;
    }

    private void AplicarUpgrade(RastroUpgrade upgrade)
    {
        if (personagemSendoUprado == null || upgrade == null) return;
        foreach (var modifier in upgrade.modifiers)
        {
            switch (modifier.statToModify)
            {
                case CharacterStatType.MaxHealth:
                    if (modifier.modType == ModificationType.Additive)
                        personagemSendoUprado.maxHealth += modifier.value;
                    else
                        personagemSendoUprado.maxHealth *= (1 + modifier.value);
                    break;
                case CharacterStatType.Damage:
                    if (modifier.modType == ModificationType.Additive)
                        personagemSendoUprado.damage += modifier.value;
                    else
                        personagemSendoUprado.damage *= (1 + modifier.value);
                    break;
                case CharacterStatType.MoveSpeed:
                    if (modifier.modType == ModificationType.Additive)
                        personagemSendoUprado.moveSpeed += modifier.value;
                    else
                        personagemSendoUprado.moveSpeed *= (1 + modifier.value);
                    break;
            }
        }
    }

    public void AtualizarUI()
    {
        if (textoPontosDisponiveis != null)
        {
            textoPontosDisponiveis.text = "Pontos: " + (personagemSendoUprado?.pontosRastrosDisponiveis ?? 0);
        }
    }

    public void AtualizarEstadoBotoes()
    {
        foreach (var botao in todosBotoesDaArvore)
        {
            if (botao != null)
            {
                botao.VerificarEstado(this);
            }
        }
    }
}