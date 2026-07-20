using System;
using UnityEditor;
using UnityEngine;
using ExoBeasts.ExoConfig.Core;

/// <summary>
/// Utilitario one-shot: importa a config legada do EditorPrefs (formato
/// documentado em ExoLegacyPrefsMigrator, no Core) para dentro de um
/// ExoToolConfig.
///
/// Existe para a maquina do dev anterior a esta refatoracao - nesta maquina
/// (onde a Fase 2 foi escrita) o EditorPrefs esta vazio, entao Migrate()
/// degrada para "0 entidades encontradas" sem erro, exatamente como a Fase 2
/// exige. So quando alguem que ainda tem o registro antigo populado (o dev
/// anterior) rodar isso e que ele importa algo de fato.
///
/// Esta classe e so a casca impura: le EditorPrefs de verdade e repassa como
/// um delegate Func&lt;string,string&gt; para ExoLegacyPrefsMigrator.ParseEntities
/// (Core, puro, testado sem depender de EditorPrefs real). Ela tambem cuida
/// da parte que ExoEntityDefinition (Core) deliberadamente nao carrega -
/// vinculo de ExoPrefabProfile e timestamps de criacao/modificacao, que so
/// existem em ExoToolConfigEntry (ver o comentario la) - lendo essas chaves
/// extras direto do EditorPrefs aqui.
///
/// Estrategia de merge (mesma logica em Migrate abaixo): dado que o
/// ExoToolConfig.asset semeado nesta Fase 2 ja viaja versionado no git com os
/// 10 nomes reais e os 2 overrides conhecidos, e o EditorPrefs do dev
/// anterior e a fonte mais autoritativa que existe para qualquer dado que a
/// Fase 2 NAO pode conhecer (overrides adicionais, entidades extras que so
/// existiam no registro dele) - "legado vence em conflito, uniao no resto":
/// entidades que so existem no EditorPrefs sao adicionadas; overrides de
/// pasta legados sobrescrevem overrides existentes do mesmo tipo; valores
/// legados vazios nunca apagam um valor ja presente na config (evita um
/// campo vazio no registro atropelar dado bom ja semeado).
/// </summary>
public static class ExoConfigEditorPrefsMigrator
{
    /// <summary>
    /// True se QUALQUER categoria tiver uma chave de lista nao vazia no
    /// EditorPrefs. Barato de checar (3 leituras) e usado tanto pelo
    /// MenuItem (para avisar o usuario antes de migrar) quanto por quem
    /// quiser decidir se vale a pena chamar Migrate.
    /// </summary>
    public static bool HasLegacyData()
    {
        foreach (ExoCategory categoria in (ExoCategory[])Enum.GetValues(typeof(ExoCategory)))
        {
            if (!string.IsNullOrEmpty(EditorPrefs.GetString(categoria.ToString(), string.Empty)))
                return true;
        }
        return false;
    }

    [MenuItem("Exo Config/Migrar EditorPrefs (One-Shot)", false, 1100)]
    public static void MigrateMenuItem()
    {
        if (!HasLegacyData())
        {
            Debug.Log("[ExoConfig] Nenhuma config legada encontrada no EditorPrefs desta maquina. Nada para migrar.");
            return;
        }

        ExoToolConfig config = ExoToolConfig.LoadOrCreate();
        ExoBuildReport report = Migrate(config);

        foreach (ExoBuildMessage msg in report.Messages)
            Debug.Log("[ExoConfig] " + msg);

        Debug.Log("[ExoConfig] Migracao concluida. " + report.Messages.Count + " entidade(s) processada(s).");
    }

    /// <summary>
    /// Le o EditorPrefs real (via ExoLegacyPrefsMigrator.ParseEntities +
    /// leituras diretas de Profile/Created/Modified) e faz merge no
    /// ExoToolConfig recebido. Nao salva um novo asset nem substitui
    /// "config" por outra instancia - muta a instancia recebida via a API
    /// publica de ExoToolConfig (que ja cuida de marcar sujo/salvar a cada
    /// mutacao).
    /// </summary>
    public static ExoBuildReport Migrate(ExoToolConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        ExoBuildReport report = new ExoBuildReport();

        var legado = ExoLegacyPrefsMigrator.ParseEntities(
            chave => EditorPrefs.GetString(chave, string.Empty),
            report);

        foreach (ExoEntityDefinition definicao in legado)
        {
            if (!ExoCategoryParser.TryParse(definicao.Categoria, out ExoCategory categoria))
            {
                report.Warning("Categoria legada invalida, entidade ignorada.", definicao.Nome);
                continue;
            }

            config.AddEntity(categoria, definicao.Nome);

            foreach (ExoFolderOverride overr in definicao.FolderOverrides)
            {
                if (!ExoAssetTypeParser.TryParse(overr.Tipo, out ExoAssetType tipo))
                    continue;

                // Legado vence em conflito: sobrescreve qualquer override
                // semeado para o mesmo (categoria, nome, tipo).
                config.SetFolderOverride(categoria, definicao.Nome, tipo, overr.Pasta);
            }

            string prefixo = categoria + "_" + definicao.Nome + "_";

            string profilePath = EditorPrefs.GetString(prefixo + "Profile", string.Empty);
            if (!string.IsNullOrEmpty(profilePath))
                config.SetProfileAssetPath(categoria, definicao.Nome, profilePath);

            AplicarTimestampLegado(config, categoria, definicao.Nome, "Created_" + categoria + "_" + definicao.Nome, ehCriacao: true);
            AplicarTimestampLegado(config, categoria, definicao.Nome, "Modified_" + categoria + "_" + definicao.Nome, ehCriacao: false);
        }

        // AplicarTimestampLegado muta ExoToolConfigEntry.CreatedTicks/ModifiedTicks
        // diretamente (campos publicos), sem passar pelos metodos de mutacao
        // de ExoToolConfig (que marcam sujo/salvam a cada chamada) - por
        // simetria com como ExoEntityDefinition/ExoFolderOverride (Core) ja
        // sao "sacolas de campos publicos" mutadas diretamente em varios
        // lugares deste arquivo. Sem este SetDirty+Save final, so os
        // timestamps ficariam pendurados em memoria (sujos, mas nunca
        // gravados no .asset) enquanto entidades/overrides/perfil - que
        // passam por AddEntity/SetFolderOverride/SetProfileAssetPath, e essas
        // ja chamam MarkDirty internamente - ja estariam persistidos.
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);

        return report;
    }

    private static void AplicarTimestampLegado(ExoToolConfig config, ExoCategory categoria, string nome, string chave, bool ehCriacao)
    {
        string valor = EditorPrefs.GetString(chave, string.Empty);
        if (string.IsNullOrEmpty(valor))
            return;

        if (!long.TryParse(valor, out long ticks))
            return;

        ExoToolConfigEntry entry = config.FindEntry(categoria, nome);
        if (entry == null)
            return;

        if (ehCriacao)
            entry.CreatedTicks = ticks;
        else
            entry.ModifiedTicks = ticks;
    }
}
