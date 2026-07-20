namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// As 3 categorias reais da ferramenta Exo Config.
    ///
    /// Estes nomes sao as chaves usadas hoje no EditorPrefs (ver
    /// ExoConfigWindow.TABS) e o parametro "categoria" de
    /// ExoPrefabMenu.ExecutarOrganizar / ExoPrefabBuilder.BuildCharacterPrefab.
    ///
    /// Nao renomear os membros: o nome do enum (ExoCategory.X.ToString()) precisa
    /// bater exatamente com a chave de EditorPrefs (legado) e e usado como
    /// segmento de caminho no picker "Assets/Exo Prefabs/Organizar..." (ver
    /// ExoPickerItemBuilder.BuildItems, no Core, e
    /// Assets/Editor/ExoPrefabMenu.cs).
    /// </summary>
    public enum ExoCategory
    {
        Personagens,
        Monstros,
        Environment
    }
}
