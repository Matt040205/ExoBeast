using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ── SlotEquipeUI ───────────────────────────────────────
/// Controla o visual e interatividade individual de cada slot na grade de equipe.
///  ▸ Garante que o clique em qualquer ponto do slot/imagem selecione a personagem.
/// ───────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Button))]
public class SlotEquipeUI : MonoBehaviour
{
    [Header("Referências Visuais")]
    public Image imagemDoPersonagem;
    public Image molduraDeFundo; // Exibe a cor do player ou background do slot

    private Button _botaoPrincipal;

    private void Awake()
    {
        GarantirBotaoERaycasts();
    }

    private void OnEnable()
    {
        GarantirBotaoERaycasts();
    }

    /// <summary>
    /// Configura o Button no objeto raiz e garante raycastTarget ativo na imagem do personagem e moldura
    /// para que qualquer clique na área do slot acione o botão principal.
    /// </summary>
    public void GarantirBotaoERaycasts()
    {
        _botaoPrincipal = GetComponent<Button>();
        if (_botaoPrincipal == null)
            _botaoPrincipal = gameObject.AddComponent<Button>();

        _botaoPrincipal.interactable = true;

        if (imagemDoPersonagem != null)
        {
            imagemDoPersonagem.raycastTarget = true;

            // Se a imagem tiver um Button filho por engano, desativa para o clique ir para o botão pai
            Button btnFilho = imagemDoPersonagem.GetComponent<Button>();
            if (btnFilho != null) btnFilho.enabled = false;
        }

        if (molduraDeFundo != null)
        {
            molduraDeFundo.raycastTarget = true;
        }
        else
        {
            Image mainImage = GetComponent<Image>();
            if (mainImage != null) mainImage.raycastTarget = true;
        }
    }

    public void LimparSlot()
    {
        GarantirBotaoERaycasts();
        if (imagemDoPersonagem != null)
        {
            imagemDoPersonagem.sprite = null;
            imagemDoPersonagem.gameObject.SetActive(false);
        }
    }

    public void SetPersonagem(CharacterBase personagem)
    {
        GarantirBotaoERaycasts();
        if (personagem == null)
        {
            LimparSlot();
            return;
        }

        if (imagemDoPersonagem != null)
        {
            imagemDoPersonagem.sprite = personagem.characterIcon;
            imagemDoPersonagem.gameObject.SetActive(true);
        }
    }

    public void DefinirCorDoJogador(Color corDoPlayer)
    {
        if (molduraDeFundo != null)
        {
            molduraDeFundo.color = corDoPlayer;
        }

        Image[] imagens = GetComponentsInChildren<Image>(true);
        foreach (Image imagem in imagens)
        {
            if (imagem == null) continue;

            if (imagem.gameObject.name == "BordaOverlay" || imagem.gameObject.name == "CoroaOverlay")
            {
                imagem.color = corDoPlayer;
            }
        }
    }
}
