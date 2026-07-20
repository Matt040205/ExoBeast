using System;
using System.Collections.Generic;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Override de pasta para um tipo de asset especifico de uma entidade.
    /// "Tipo" usa os mesmos nomes de ExoAssetType (Materiais, Modelos, Texturas,
    /// Prefabs, Animacao) como string. Fica como string (em vez de referenciar o
    /// enum ExoAssetType diretamente) para manter ExoEntityDefinition como um
    /// DTO so com dados primitivos, tolerante a valores legados/nao validados -
    /// quem consumir a definicao decide como e quando validar/converter.
    /// </summary>
    [Serializable]
    public class ExoFolderOverride
    {
        public string Tipo;
        public string Pasta;

        public ExoFolderOverride()
        {
        }

        public ExoFolderOverride(string tipo, string pasta)
        {
            Tipo = tipo;
            Pasta = pasta;
        }
    }

    /// <summary>
    /// Definicao de uma entidade da Exo Config, contendo apenas dados primitivos
    /// (strings/paths): nome, categoria e a lista de overrides de pasta por tipo
    /// de asset.
    ///
    /// Deliberadamente NAO referencia ExoPrefabProfile, CharacterBase ou qualquer
    /// outro tipo de jogo/UnityEngine - isso violaria o asmdef sem dependencias
    /// deste assembly (ver ExoBeasts.ExoConfig.Core.asmdef, references: []).
    ///
    /// "Categoria" fica como string (nao como o enum ExoCategory) de proposito:
    /// esta classe existe para transportar/serializar dados crus (por exemplo, um
    /// snapshot do que hoje mora no EditorPrefs), e uma string crua tolera
    /// valores legados ou ainda nao validados sem lancar excecao. A camada que
    /// efetivamente usa a definicao (fora deste assembly) e responsavel por
    /// validar/converter para ExoCategory antes de chamar ExoPathResolver.
    /// </summary>
    [Serializable]
    public class ExoEntityDefinition
    {
        public string Nome;
        public string Categoria;
        public List<ExoFolderOverride> FolderOverrides = new List<ExoFolderOverride>();
    }
}
