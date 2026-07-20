using System.Collections.Generic;
using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoOverrideMapBuilderTests
{
    [Test]
    public void Build_NullInput_ReturnsEmptyDictionaryWithoutThrowing()
    {
        Dictionary<ExoPathOverrideKey, string> mapa = ExoOverrideMapBuilder.Build(null);
        Assert.That(mapa, Is.Not.Null);
        Assert.That(mapa, Is.Empty);
    }

    [Test]
    public void Build_EmptyInput_ReturnsEmptyDictionary()
    {
        Dictionary<ExoPathOverrideKey, string> mapa = ExoOverrideMapBuilder.Build(new List<ExoEntityDefinition>());
        Assert.That(mapa, Is.Empty);
    }

    [Test]
    public void Build_ValidOverride_ProducesKeyThatResolveFolderAccepts()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition
            {
                Nome = "Sylvie",
                Categoria = "Personagens",
                FolderOverrides = new List<ExoFolderOverride>
                {
                    new ExoFolderOverride("Animacao", "Assets/Personagens/Sylvie/Animação/Terranomas/Arqueira")
                }
            }
        };

        Dictionary<ExoPathOverrideKey, string> mapa = ExoOverrideMapBuilder.Build(definicoes);

        Assert.That(mapa.Count, Is.EqualTo(1));
        string resolved = ExoPathResolver.ResolveFolder(ExoCategory.Personagens, "Sylvie", ExoAssetType.Animacao, mapa);
        Assert.That(resolved, Is.EqualTo("Assets/Personagens/Sylvie/Animação/Terranomas/Arqueira"));
    }

    [Test]
    public void Build_MultipleEntitiesAndTipos_AllKeysPresent()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition
            {
                Nome = "Águia",
                Categoria = "Monstros",
                FolderOverrides = new List<ExoFolderOverride>
                {
                    new ExoFolderOverride("Prefabs", "Assets/Entidades/Inimigos")
                }
            },
            new ExoEntityDefinition
            {
                Nome = "Aranha",
                Categoria = "Monstros",
                FolderOverrides = new List<ExoFolderOverride>
                {
                    new ExoFolderOverride("Prefabs", "Assets/Entidades/Inimigos")
                }
            }
        };

        Dictionary<ExoPathOverrideKey, string> mapa = ExoOverrideMapBuilder.Build(definicoes);

        Assert.That(mapa.Count, Is.EqualTo(2));
        Assert.That(mapa[new ExoPathOverrideKey(ExoCategory.Monstros, "Águia", ExoAssetType.Prefabs)], Is.EqualTo("Assets/Entidades/Inimigos"));
        Assert.That(mapa[new ExoPathOverrideKey(ExoCategory.Monstros, "Aranha", ExoAssetType.Prefabs)], Is.EqualTo("Assets/Entidades/Inimigos"));
    }

    [Test]
    public void Build_UnknownCategoria_SkipsEntityAndReportsWarning()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition
            {
                Nome = "X",
                Categoria = "NaoExisteEssaCategoria",
                FolderOverrides = new List<ExoFolderOverride> { new ExoFolderOverride("Materiais", "Assets/X") }
            }
        };

        ExoBuildReport report = new ExoBuildReport();
        Dictionary<ExoPathOverrideKey, string> mapa = ExoOverrideMapBuilder.Build(definicoes, report);

        Assert.That(mapa, Is.Empty);
        Assert.That(report.HasWarnings, Is.True);
        Assert.That(report.HasErrors, Is.False);
    }

    [Test]
    public void Build_UnknownTipo_SkipsOnlyThatOverride()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition
            {
                Nome = "Ayame",
                Categoria = "Personagens",
                FolderOverrides = new List<ExoFolderOverride>
                {
                    new ExoFolderOverride("TipoQueNaoExiste", "Assets/Lixo"),
                    new ExoFolderOverride("Materiais", "Assets/Custom/AyameMat")
                }
            }
        };

        ExoBuildReport report = new ExoBuildReport();
        Dictionary<ExoPathOverrideKey, string> mapa = ExoOverrideMapBuilder.Build(definicoes, report);

        Assert.That(mapa.Count, Is.EqualTo(1));
        Assert.That(mapa[new ExoPathOverrideKey(ExoCategory.Personagens, "Ayame", ExoAssetType.Materiais)], Is.EqualTo("Assets/Custom/AyameMat"));
        Assert.That(report.HasWarnings, Is.True);
    }

    [Test]
    public void Build_EmptyOrNullPastaIsIgnored()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition
            {
                Nome = "Ayame",
                Categoria = "Personagens",
                FolderOverrides = new List<ExoFolderOverride>
                {
                    new ExoFolderOverride("Materiais", ""),
                    new ExoFolderOverride("Modelos", null)
                }
            }
        };

        Dictionary<ExoPathOverrideKey, string> mapa = ExoOverrideMapBuilder.Build(definicoes);
        Assert.That(mapa, Is.Empty);
    }

    [Test]
    public void Build_EntityWithNoOverrides_ProducesNoKeys()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "Ponte", Categoria = "Environment" }
        };

        Dictionary<ExoPathOverrideKey, string> mapa = ExoOverrideMapBuilder.Build(definicoes);
        Assert.That(mapa, Is.Empty);
    }
}
