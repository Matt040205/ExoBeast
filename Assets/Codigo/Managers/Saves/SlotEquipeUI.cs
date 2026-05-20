using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ── SlotEquipeUI ───────────────────────────────────────
/// Controla o visual individual de cada slot na grade de equipe.
/// 
///  ▸ Alterna entre o ícone de "+" (vazio) e a foto do personagem.
/// ───────────────────────────────────────────────────────
/// </summary>
public class SlotEquipeUI : MonoBehaviour
{
    [Header("Referências Visuais")]
    public Image imagemDoPersonagem;
    public GameObject iconeSinalMais;
    public Image molduraDeFundo; // Adicionado para exibir a cor do player

    public void LimparSlot()
    {
        if (imagemDoPersonagem != null) imagemDoPersonagem.gameObject.SetActive(false);
        if (iconeSinalMais != null) iconeSinalMais.SetActive(true);
    }

    public void SetPersonagem(CharacterBase personagem)
    {
        if (personagem == null)
        {
            LimparSlot();
            return;
        }

        if (iconeSinalMais != null) iconeSinalMais.SetActive(false);

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
            if (imagem == null)
                continue;

            if (imagem.gameObject.name == "BordaOverlay" || imagem.gameObject.name == "CoroaOverlay")
            {
                imagem.color = corDoPlayer;
            }
        }
    }
}
