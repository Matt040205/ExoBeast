using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoLegacyPrefsMigratorTests
{
    private static Func<string, string> FromDict(Dictionary<string, string> dict)
    {
        return chave => dict.TryGetValue(chave, out string valor) ? valor : null;
    }

    [Test]
    public void ParseEntities_NullDelegate_ReturnsEmptyListWithoutThrowing()
    {
        List<ExoEntityDefinition> resultado = ExoLegacyPrefsMigrator.ParseEntities(null);
        Assert.That(resultado, Is.Not.Null);
        Assert.That(resultado, Is.Empty);
    }

    /// <summary>
    /// Degradacao exigida pela Fase 2: nesta maquina o EditorPrefs do dev
    /// anterior nao existe - todo GetString cairia no default (string
    /// vazia). O migrador tem que devolver lista vazia sem lancar excecao
    /// nesse cenario.
    /// </summary>
    [Test]
    public void ParseEntities_AllKeysMissing_DegradesToEmptyListWithoutThrowing()
    {
        List<ExoEntityDefinition> resultado = ExoLegacyPrefsMigrator.ParseEntities(chave => null);
        Assert.That(resultado, Is.Empty);

        resultado = ExoLegacyPrefsMigrator.ParseEntities(chave => "");
        Assert.That(resultado, Is.Empty);
    }

    [Test]
    public void ParseEntities_ParsesListAndFolderOverrides()
    {
        Dictionary<string, string> dict = new Dictionary<string, string>
        {
            ["Personagens"] = "Ayame,Brunhilde",
            ["Personagens_Ayame_Mat"] = "Assets/Custom/AyameMat",
            ["Personagens_Ayame_Ani"] = "Assets/Custom/AyameAni",
        };

        List<ExoEntityDefinition> resultado = ExoLegacyPrefsMigrator.ParseEntities(FromDict(dict));

        Assert.That(resultado.Count, Is.EqualTo(2));

        ExoEntityDefinition ayame = resultado.Single(e => e.Nome == "Ayame");
        Assert.That(ayame.Categoria, Is.EqualTo("Personagens"));
        Assert.That(ayame.FolderOverrides.Count, Is.EqualTo(2));
        Assert.That(ayame.FolderOverrides.Any(o => o.Tipo == "Materiais" && o.Pasta == "Assets/Custom/AyameMat"));
        Assert.That(ayame.FolderOverrides.Any(o => o.Tipo == "Animacao" && o.Pasta == "Assets/Custom/AyameAni"));

        ExoEntityDefinition brunhilde = resultado.Single(e => e.Nome == "Brunhilde");
        Assert.That(brunhilde.FolderOverrides, Is.Empty);
    }

    [Test]
    public void ParseEntities_AllFiveSuffixesMapToCorrectExoAssetType()
    {
        Dictionary<string, string> dict = new Dictionary<string, string>
        {
            ["Personagens"] = "Ayame",
            ["Personagens_Ayame_Mat"] = "Assets/M",
            ["Personagens_Ayame_Mod"] = "Assets/Mo",
            ["Personagens_Ayame_Tex"] = "Assets/T",
            ["Personagens_Ayame_Pre"] = "Assets/P",
            ["Personagens_Ayame_Ani"] = "Assets/A",
        };

        ExoEntityDefinition ayame = ExoLegacyPrefsMigrator.ParseEntities(FromDict(dict)).Single();

        Assert.That(ayame.FolderOverrides.Count, Is.EqualTo(5));
        Assert.That(ayame.FolderOverrides.Single(o => o.Tipo == "Materiais").Pasta, Is.EqualTo("Assets/M"));
        Assert.That(ayame.FolderOverrides.Single(o => o.Tipo == "Modelos").Pasta, Is.EqualTo("Assets/Mo"));
        Assert.That(ayame.FolderOverrides.Single(o => o.Tipo == "Texturas").Pasta, Is.EqualTo("Assets/T"));
        Assert.That(ayame.FolderOverrides.Single(o => o.Tipo == "Prefabs").Pasta, Is.EqualTo("Assets/P"));
        Assert.That(ayame.FolderOverrides.Single(o => o.Tipo == "Animacao").Pasta, Is.EqualTo("Assets/A"));
    }

    /// <summary>
    /// ExoPathResolver.SupportsAssetType diz que Environment nao suporta
    /// Animacao. Um valor legado "Environment_Ponte_Ani" perdido no
    /// EditorPrefs (de uma config antiga/invalida) nao deve virar override -
    /// ExoPathResolver.ResolveFolder lançaria InvalidOperationException antes
    /// de sequer olhar pros overrides nesse caso, entao gravar esse override
    /// seria so lixo morto no ExoToolConfig resultante.
    /// </summary>
    [Test]
    public void ParseEntities_SkipsAnimacaoOverrideForEnvironment()
    {
        Dictionary<string, string> dict = new Dictionary<string, string>
        {
            ["Environment"] = "Ponte",
            ["Environment_Ponte_Ani"] = "Assets/Deveria/Ser/Ignorado",
            ["Environment_Ponte_Mat"] = "Assets/Mapas/Ponte/Materiais",
        };

        ExoEntityDefinition ponte = ExoLegacyPrefsMigrator.ParseEntities(FromDict(dict)).Single();

        Assert.That(ponte.FolderOverrides.Any(o => o.Tipo == "Animacao"), Is.False);
        Assert.That(ponte.FolderOverrides.Single(o => o.Tipo == "Materiais").Pasta, Is.EqualTo("Assets/Mapas/Ponte/Materiais"));
    }

    [Test]
    public void ParseEntities_ReadsAllThreeCategorias()
    {
        Dictionary<string, string> dict = new Dictionary<string, string>
        {
            ["Personagens"] = "Ayame",
            ["Monstros"] = "Aranha",
            ["Environment"] = "Ponte",
        };

        List<ExoEntityDefinition> resultado = ExoLegacyPrefsMigrator.ParseEntities(FromDict(dict));

        Assert.That(resultado.Select(e => e.Categoria), Is.EquivalentTo(new[] { "Personagens", "Monstros", "Environment" }));
        Assert.That(resultado.Select(e => e.Nome), Is.EquivalentTo(new[] { "Ayame", "Aranha", "Ponte" }));
    }

    [Test]
    public void ParseEntities_IgnoresEmptyEntriesInCsvList()
    {
        Dictionary<string, string> dict = new Dictionary<string, string>
        {
            ["Personagens"] = "Ayame,,Brunhilde,",
        };

        List<ExoEntityDefinition> resultado = ExoLegacyPrefsMigrator.ParseEntities(FromDict(dict));

        Assert.That(resultado.Select(e => e.Nome), Is.EquivalentTo(new[] { "Ayame", "Brunhilde" }));
    }

    [Test]
    public void ParseEntities_ReportReceivesInfoPerEntity()
    {
        Dictionary<string, string> dict = new Dictionary<string, string> { ["Personagens"] = "Ayame,Brunhilde" };
        ExoBuildReport report = new ExoBuildReport();

        ExoLegacyPrefsMigrator.ParseEntities(FromDict(dict), report);

        Assert.That(report.Messages.Count, Is.EqualTo(2));
        Assert.That(report.HasErrors, Is.False);
    }
}
