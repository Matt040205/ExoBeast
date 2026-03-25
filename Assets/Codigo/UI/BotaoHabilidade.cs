using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public struct MapeamentoUpgradePersonagem
{
    public CharacterBase personagemSO;
    public RastroUpgrade upgradeSO;
}


[RequireComponent(typeof(Button))]
public class BotaoHabilidade : MonoBehaviour
{
    [Header("Identificação da Habilidade")]
    public string idHabilidade;
    public string idCaminho;

    [Header("Requisitos para Desbloquear")]
    public int custo = 1;
    public string preRequisitoHabilidade;
    public int pontosGlobaisNecessarios = 0;
    public int pontosNoCaminhoNecessarios = 0;

    [Header("Mapeamento de Upgrades")]
    public List<MapeamentoUpgradePersonagem> upgradesPorPersonagem;

    [Header("Referências")]
    public Rastros managerRastros;
    private Button meuBotao;
    public Image iconeHabilidade;
    public Image bordaImage;

    [Header("Cores de Estado")]
    public UnityEngine.Color corBloqueado = new UnityEngine.Color(0.5f, 0.5f, 0.5f, 1f);
    public UnityEngine.Color corDisponivel = UnityEngine.Color.white;
    public UnityEngine.Color corDesbloqueado = UnityEngine.Color.green;

    private Coroutine blinkingCoroutine;

    void Awake()
    {
        meuBotao = GetComponent<Button>();
        meuBotao.onClick.RemoveAllListeners();
        meuBotao.onClick.AddListener(OnBotaoClicado);

        if (managerRastros == null)
        {
            managerRastros = FindObjectOfType<Rastros>();
        }

        if (managerRastros != null)
        {
            if (!managerRastros.todosBotoesDaArvore.Contains(this))
            {
                managerRastros.todosBotoesDaArvore.Add(this);
            }
        }
    }

    void Start()
    {
        if (managerRastros != null)
        {
            VerificarEstado(managerRastros);
        }
    }

    void OnBotaoClicado()
    {
        if (managerRastros != null)
        {
            managerRastros.SelecionarHabilidade(this);
        }
        StopBlinking();
    }

    public RastroUpgrade GetUpgradeParaPersonagem(CharacterBase personagemAtivo)
    {
        if (personagemAtivo == null) return null;

        foreach (var map in upgradesPorPersonagem)
        {
            if (map.personagemSO == null) continue;

            if (personagemAtivo.name.StartsWith(map.personagemSO.name))
            {
                return map.upgradeSO;
            }
        }
        return null;
    }

    public void VerificarEstado(Rastros manager)
    {
        if (meuBotao == null) meuBotao = GetComponent<Button>();

        if (manager == null || manager.habilidadesDesbloqueadas == null || manager.pontosPorCaminho == null)
        {
            if (managerRastros == null)
                managerRastros = FindObjectOfType<Rastros>();

            if (managerRastros == null || managerRastros.habilidadesDesbloqueadas == null)
            {
                if (meuBotao != null) meuBotao.interactable = false;
                return;
            }
            manager = managerRastros;
        }

        StopBlinking();
        if (bordaImage != null)
        {
            bordaImage.gameObject.SetActive(false);
        }

        ColorBlock colors = meuBotao.colors;

        if (manager.habilidadesDesbloqueadas.Contains(idHabilidade))
        {
            if (iconeHabilidade != null) iconeHabilidade.color = corDesbloqueado;
            colors.disabledColor = corDesbloqueado;
            meuBotao.interactable = false;

            if (bordaImage != null)
            {
                bordaImage.gameObject.SetActive(true);
                bordaImage.color = corDesbloqueado;
            }
        }
        else
        {
            bool preRequisitosOk = true;
            if (!string.IsNullOrEmpty(preRequisitoHabilidade) && !manager.habilidadesDesbloqueadas.Contains(preRequisitoHabilidade))
                preRequisitosOk = false;

            if (manager.pontosGastosGlobal < pontosGlobaisNecessarios)
                preRequisitosOk = false;

            int pontosAtuaisCaminho = 0;
            foreach (var caminho in manager.pontosPorCaminho)
            {
                if (caminho.idCaminho == idCaminho)
                {
                    pontosAtuaisCaminho = caminho.pontosGastos;
                    break;
                }
            }
            if (pontosAtuaisCaminho < pontosNoCaminhoNecessarios)
                preRequisitosOk = false;

            bool temPontos = manager.pontosDisponiveis >= custo;

            if (preRequisitosOk)
            {
                if (iconeHabilidade != null) iconeHabilidade.color = corBloqueado;
                colors.normalColor = corBloqueado;
                colors.highlightedColor = corDisponivel;
                colors.pressedColor = new UnityEngine.Color(0.9f, 0.9f, 0.9f);
                colors.selectedColor = corBloqueado;
                meuBotao.interactable = temPontos;

                if (bordaImage != null)
                {
                    bordaImage.gameObject.SetActive(true);
                    bordaImage.color = corDisponivel;
                }
            }
            else
            {
                if (iconeHabilidade != null) iconeHabilidade.color = corBloqueado;
                colors.disabledColor = corBloqueado;
                meuBotao.interactable = false;

                if (bordaImage != null)
                {
                    bordaImage.gameObject.SetActive(false);
                }
            }
        }
        meuBotao.colors = colors;
    }
    private void StartBlinking()
    {
        if (blinkingCoroutine == null && gameObject.activeInHierarchy)
        {
            blinkingCoroutine = StartCoroutine(BlinkEffect());
        }
    }

    private void StopBlinking()
    {
        if (blinkingCoroutine != null)
        {
            StopCoroutine(blinkingCoroutine);
            blinkingCoroutine = null;
        }

        if (iconeHabilidade != null)
        {
            UnityEngine.Color c = iconeHabilidade.color;
            c.a = 1f;
            iconeHabilidade.color = c;
        }
    }

    private IEnumerator BlinkEffect()
    {
        if (iconeHabilidade == null) yield break;

        float pulseSpeed = 2f;
        UnityEngine.Color baseColor = iconeHabilidade.color;

        while (true)
        {
            float alpha = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            alpha = Mathf.Lerp(0.3f, 1.0f, alpha);

            baseColor.a = alpha;
            iconeHabilidade.color = baseColor;
            yield return null;
        }
    }
}